using System.Text.Json;

namespace RankUp.Models;

public static class ItemDatabase
{
    private const string PrefetchFlagKey = "items_prefetched_v1";
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly Dictionary<int, string> Names = new();
    private static bool _loaded = false;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static int Count => Names.Count;

    public static async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;

            try
            {
                var json = await _http.GetStringAsync("https://api.opendota.com/api/constants/items");
                var root = JsonDocument.Parse(json).RootElement;

                foreach (var prop in root.EnumerateObject())
                {
                    if (!prop.Value.TryGetProperty("id", out var idProp)) continue;
                    if (idProp.ValueKind != JsonValueKind.Number) continue;

                    var id = idProp.GetInt32();
                    if (id <= 0) continue;

                    Names[id] = prop.Name;
                }

                System.Diagnostics.Debug.WriteLine($"[ItemDatabase] Загружено {Names.Count} имён");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemDatabase] Ошибка: {ex.Message}");
            }
        }
        finally
        {
            _loaded = true;
            _lock.Release();
        }
    }

    public static string GetIconUrl(int itemId)
    {
        if (itemId <= 0) return "";
        if (!Names.TryGetValue(itemId, out var name)) return "";

        var local = ImageCache.ItemPath(name);
        var remote = $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/items/{name}.png";
        return ImageCache.Source(local, remote);
    }

    // ═══════════════════════════════════════════
    // ПРЕДЗАГРУЗКА ВСЕХ ИКОНОК ПРЕДМЕТОВ
    // ═══════════════════════════════════════════
    public static async Task PrefetchAllAsync()
    {
        if (Preferences.Default.Get(PrefetchFlagKey, false)) return;

        try
        {
            // Сначала грузим имена, чтобы знать что качать
            await EnsureLoadedAsync();
            if (Names.Count == 0) return;

            ImageCache.EnsureDirs();

            var names = Names.Values.Distinct().ToList();
            System.Diagnostics.Debug.WriteLine($"[ItemPrefetch] Всего предметов: {names.Count}");

            int done = 0;
            var sem = new SemaphoreSlim(12);

            var tasks = names.Select(async name =>
            {
                await sem.WaitAsync();
                try
                {
                    var url = $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/items/{name}.png";
                    var path = ImageCache.ItemPath(name);
                    if (await ImageCache.DownloadAsync(_http, url, path))
                        Interlocked.Increment(ref done);
                }
                finally
                {
                    sem.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            System.Diagnostics.Debug.WriteLine($"[ItemPrefetch] Скачано: {done}/{names.Count}");

            if (done > 0)
                Preferences.Default.Set(PrefetchFlagKey, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ItemPrefetch] Ошибка: {ex.Message}");
        }
    }
}