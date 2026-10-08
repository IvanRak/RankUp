using System.Text.Json;

namespace RankUp.Models;

public static class ItemDatabase
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly Dictionary<int, string> Names = new();
    private static bool _loaded = false;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private static string LocalFile => Path.Combine(FileSystem.AppDataDirectory, "items.json");
    private static string PrefetchFlagKey => "items_prefetched_v4";

    public static int Count => Names.Count;

    public static async Task EnsureLoadedAsync()
    {
        if (_loaded && Names.Count > 0) return;

        await _lock.WaitAsync();
        try
        {
            if (_loaded && Names.Count > 0) return;

            // 1. Из локальной копии в AppData (быстрее, чем читать из APK)
            if (File.Exists(LocalFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(LocalFile);
                    ParseJson(json);
                    if (Names.Count > 0)
                    {
                        _loaded = true;
                        System.Diagnostics.Debug.WriteLine($"[ItemDatabase] Из файла AppData: {Names.Count}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ItemDatabase] AppData ошибка: {ex.Message}");
                }
            }

            // 2. Из встроенного ресурса Resources/Raw/items_baked.json
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("items_baked.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                ParseJson(json);

                if (Names.Count > 0)
                {
                    await File.WriteAllTextAsync(LocalFile, json);
                    _loaded = true;
                    System.Diagnostics.Debug.WriteLine($"[ItemDatabase] Из ресурса: {Names.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ItemDatabase] Ресурс есть, но распарсилось 0");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemDatabase] Ресурс не найден: {ex.Message}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static void ParseJson(string json)
    {
        try
        {
            var root = JsonDocument.Parse(json).RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (root.TryGetProperty("error", out _)) return;

            foreach (var prop in root.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("id", out var idProp)) continue;
                if (idProp.ValueKind != JsonValueKind.Number) continue;

                var id = idProp.GetInt32();
                if (id <= 0) continue;

                Names[id] = prop.Name;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ItemDatabase] Parse error: {ex.Message}");
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

    // ─── ПРЕДЗАГРУЗКА ИКОНОК (только CDN) ───
    public static async Task PrefetchAllAsync()
    {
        if (Preferences.Default.Get(PrefetchFlagKey, false)) return;

        await EnsureLoadedAsync();
        if (Names.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[ItemPrefetch] Справочник пуст");
            return;
        }

        try
        {
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

            if (done >= names.Count * 0.8)
                Preferences.Default.Set(PrefetchFlagKey, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ItemPrefetch] Ошибка: {ex.Message}");
        }
    }

    public static void ClearCache()
    {
        try
        {
            if (File.Exists(LocalFile)) File.Delete(LocalFile);
            Preferences.Default.Remove(PrefetchFlagKey);
        }
        catch { }
    }
}