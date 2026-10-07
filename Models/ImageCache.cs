namespace RankUp.Models;

public static class ImageCache
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static string RootDir => Path.Combine(FileSystem.AppDataDirectory, "icons");
    public static string HeroesDir => Path.Combine(RootDir, "heroes");
    public static string ItemsDir => Path.Combine(RootDir, "items");

    public static void EnsureDirs()
    {
        Directory.CreateDirectory(HeroesDir);
        Directory.CreateDirectory(ItemsDir);
    }

    public static string HeroPath(string slug) => Path.Combine(HeroesDir, $"{slug}.png");
    public static string ItemPath(string name) => Path.Combine(ItemsDir, $"{name}.png");

    public static bool HeroExists(string slug) => File.Exists(HeroPath(slug));
    public static bool ItemExists(string name) => File.Exists(ItemPath(name));

    /// <summary>
    /// Возвращает локальный путь если есть, иначе удалённый URL.
    /// MAUI Image понимает и то, и то.
    /// </summary>
    public static string Source(string localPath, string remoteUrl)
    {
        return File.Exists(localPath) ? localPath : remoteUrl;
    }

    public static async Task<bool> DownloadAsync(HttpClient http, string url, string localPath)
    {
        if (File.Exists(localPath)) return true;
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            if (bytes.Length == 0) return false;
            await File.WriteAllBytesAsync(localPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}