using System.Text.Json;
using RankUp.Models;
using SQLite;

namespace RankUp.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_db is not null) return _db;

        var path = Path.Combine(
            FileSystem.AppDataDirectory,
            "rankup.db3");

        _db = new SQLiteAsyncConnection(path, SQLiteOpenFlags.ReadWrite |
                                              SQLiteOpenFlags.Create |
                                              SQLiteOpenFlags.SharedCache);

        await _db.CreateTableAsync<MatchCache>();
        return _db;
    }

    // ═══════════════════════════════════════════
    // ЧТЕНИЕ МАТЧЕЙ
    // ═══════════════════════════════════════════
    public async Task<List<DotaMatch>> GetMatchesAsync(long accountId)
    {
        try
        {
            var db = await GetConnectionAsync();

            var cached = await db.Table<MatchCache>()
                .Where(m => m.AccountId == accountId)
                .OrderByDescending(m => m.StartTime)
                .ToListAsync();

            return cached.Select(ToDotaMatch).ToList();
        }
        catch
        {
            return new List<DotaMatch>();
        }
    }

    // ═══════════════════════════════════════════
    // СОХРАНЕНИЕ ПАЧКИ МАТЧЕЙ
    // ═══════════════════════════════════════════
    public async Task SaveMatchesAsync(long accountId, List<DotaMatch> matches)
    {
        if (matches is null || matches.Count == 0) return;

        try
        {
            var db = await GetConnectionAsync();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var toSave = new List<MatchCache>();

            foreach (var m in matches)
            {
                // Не перезаписываем уже существующие поля, если пришёл "пустой" матч
                var existing = await db.Table<MatchCache>()
                    .Where(x => x.MatchId == m.MatchId)
                    .FirstOrDefaultAsync();

                if (existing is not null)
                {
                    // Обновляем только те поля, которые непустые
                    if (m.Kills > 0 || m.Deaths > 0 || m.Assists > 0)
                    {
                        existing.Kills = m.Kills;
                        existing.Deaths = m.Deaths;
                        existing.Assists = m.Assists;
                    }
                    if (m.MyPosition > 0)
                        existing.MyPosition = m.MyPosition;
                    if (!string.IsNullOrEmpty(m.MyItemsJson))
                        existing.MyItemsJson = m.MyItemsJson;

                    existing.CachedAt = now;
                    toSave.Add(existing);
                }
                else
                {
                    toSave.Add(new MatchCache
                    {
                        MatchId = m.MatchId,
                        AccountId = accountId,
                        HeroId = m.HeroId,
                        Kills = m.Kills,
                        Deaths = m.Deaths,
                        Assists = m.Assists,
                        Duration = m.Duration,
                        StartTime = m.StartTime,
                        IsWin = m.IsWin,
                        CachedAt = now,
                        AlliesJson = SerializeAllies(m.Allies),
                        MyItemsJson = m.MyItemsJson,
                        MyPosition = m.MyPosition
                    });
                }
            }

            await db.InsertAllAsync(toSave, runInTransaction: true);
        }
        catch
        {
            // молча — не валим UI
        }
    }

    // ═══════════════════════════════════════════
    // ОБНОВЛЕНИЕ KDA
    // ═══════════════════════════════════════════
    public async Task UpdateKdaAsync(long matchId, int kills, int deaths, int assists)
    {
        try
        {
            var db = await GetConnectionAsync();

            var row = await db.Table<MatchCache>()
                .Where(x => x.MatchId == matchId)
                .FirstOrDefaultAsync();

            if (row is null) return;

            row.Kills = kills;
            row.Deaths = deaths;
            row.Assists = assists;

            await db.UpdateAsync(row);
        }
        catch { }
    }

    // ═══════════════════════════════════════════
    // ОБНОВЛЕНИЕ СОЮЗНИКОВ
    // ═══════════════════════════════════════════
    public async Task UpdateAlliesAsync(long matchId, List<AllyInMatch> allies)
    {
        try
        {
            var db = await GetConnectionAsync();

            var row = await db.Table<MatchCache>()
                .Where(x => x.MatchId == matchId)
                .FirstOrDefaultAsync();

            if (row is null) return;

            row.AlliesJson = SerializeAllies(allies);
            await db.UpdateAsync(row);
        }
        catch { }
    }

    // ═══════════════════════════════════════════
    // ОБНОВЛЕНИЕ ПОЗИЦИИ
    // ═══════════════════════════════════════════
    public async Task UpdatePositionAsync(long matchId, int position)
    {
        try
        {
            var db = await GetConnectionAsync();

            var row = await db.Table<MatchCache>()
                .Where(x => x.MatchId == matchId)
                .FirstOrDefaultAsync();

            if (row is null) return;

            row.MyPosition = position;
            await db.UpdateAsync(row);
        }
        catch { }
    }

    // ═══════════════════════════════════════════
    // ОБНОВЛЕНИЕ ПРЕДМЕТОВ
    // ═══════════════════════════════════════════
    public async Task UpdateItemsAsync(long matchId, string itemsJson)
    {
        try
        {
            var db = await GetConnectionAsync();

            var row = await db.Table<MatchCache>()
                .Where(x => x.MatchId == matchId)
                .FirstOrDefaultAsync();

            if (row is null) return;

            row.MyItemsJson = itemsJson;
            await db.UpdateAsync(row);
        }
        catch { }
    }

    // ═══════════════════════════════════════════
    // ОЧИСТКА
    // ═══════════════════════════════════════════
    public async Task ClearAsync()
    {
        try
        {
            var db = await GetConnectionAsync();
            await db.DeleteAllAsync<MatchCache>();
        }
        catch { }
    }

    // ═══════════════════════════════════════════
    // ХЕЛПЕРЫ
    // ═══════════════════════════════════════════
    private static DotaMatch ToDotaMatch(MatchCache c)
    {
        return new DotaMatch
        {
            MatchId = c.MatchId,
            HeroId = c.HeroId,
            Kills = c.Kills,
            Deaths = c.Deaths,
            Assists = c.Assists,
            Duration = c.Duration,
            StartTime = c.StartTime,
            IsWin = c.IsWin,
            MyItemsJson = c.MyItemsJson ?? "",
            MyPosition = c.MyPosition,
            Allies = DeserializeAllies(c.AlliesJson)
        };
    }

    private static string SerializeAllies(List<AllyInMatch> allies)
    {
        if (allies is null || allies.Count == 0) return "";
        try
        {
            return JsonSerializer.Serialize(allies);
        }
        catch
        {
            return "";
        }
    }

    private static List<AllyInMatch> DeserializeAllies(string json)
    {
        if (string.IsNullOrEmpty(json)) return new List<AllyInMatch>();
        try
        {
            return JsonSerializer.Deserialize<List<AllyInMatch>>(json)
                   ?? new List<AllyInMatch>();
        }
        catch
        {
            return new List<AllyInMatch>();
        }
    }
}