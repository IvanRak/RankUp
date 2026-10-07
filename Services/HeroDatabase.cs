using System.Text.Json;

namespace RankUp.Models;

public static class HeroDatabase
{
    private const string PrefetchFlagKey = "heroes_prefetched_v1";
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static string GetDisplayName(int heroId)
    {
        if (heroId <= 0) return "Все герои";
        var slug = new DotaMatch { HeroId = heroId }.HeroName;
        if (string.IsNullOrEmpty(slug) || slug == "unknown") return "?";
        return ToDisplay(slug);
    }

    public static string GetIconUrl(int heroId)
    {
        var slug = new DotaMatch { HeroId = heroId }.HeroName;
        if (string.IsNullOrEmpty(slug) || slug == "unknown") return "";

        var local = ImageCache.HeroPath(slug);
        var remote = $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{slug}.png";
        return ImageCache.Source(local, remote);
    }

    public static string GetSlug(int heroId)
    {
        var slug = new DotaMatch { HeroId = heroId }.HeroName;
        if (string.IsNullOrEmpty(slug) || slug == "unknown") return "";
        return slug;
    }

    // ═══════════════════════════════════════════
    // ПРЕДЗАГРУЗКА ВСЕХ ИКОНОК ГЕРОЕВ
    // ═══════════════════════════════════════════
    public static async Task PrefetchAllAsync()
    {
        if (Preferences.Default.Get(PrefetchFlagKey, false)) return;

        try
        {
            ImageCache.EnsureDirs();

            var json = await _http.GetStringAsync("https://api.opendota.com/api/constants/heroes");
            var root = JsonDocument.Parse(json).RootElement;

            var slugs = new List<string>();

            foreach (var prop in root.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("name", out var nameProp)) continue;
                if (nameProp.ValueKind != JsonValueKind.String) continue;

                var fullName = nameProp.GetString() ?? ""; // "npc_dota_hero_antimage"
                if (string.IsNullOrEmpty(fullName)) continue;
                if (!fullName.StartsWith("npc_dota_hero_")) continue;

                var slug = fullName.Substring("npc_dota_hero_".Length);
                slugs.Add(slug);
            }

            System.Diagnostics.Debug.WriteLine($"[HeroPrefetch] Всего героев: {slugs.Count}");

            int done = 0;
            var sem = new SemaphoreSlim(12); // 12 параллельно

            var tasks = slugs.Select(async slug =>
            {
                await sem.WaitAsync();
                try
                {
                    var url = $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{slug}.png";
                    var path = ImageCache.HeroPath(slug);
                    if (await ImageCache.DownloadAsync(_http, url, path))
                        Interlocked.Increment(ref done);
                }
                finally
                {
                    sem.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            System.Diagnostics.Debug.WriteLine($"[HeroPrefetch] Скачано: {done}/{slugs.Count}");

            if (done > 0)
                Preferences.Default.Set(PrefetchFlagKey, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HeroPrefetch] Ошибка: {ex.Message}");
        }
    }

    // ─── ЧЕЛОВЕЧЕСКИЕ ИМЕНА ───
    private static readonly Dictionary<string, string> Exceptions = new()
    {
        { "antimage", "Anti-Mage" },
        { "centaur", "Centaur Warrunner" },
        { "doom_bringer", "Doom" },
        { "furion", "Nature's Prophet" },
        { "life_stealer", "Lifestealer" },
        { "magnataur", "Magnus" },
        { "necrolyte", "Necrophos" },
        { "nevermore", "Shadow Fiend" },
        { "obsidian_destroyer", "Outworld Destroyer" },
        { "queenofpain", "Queen of Pain" },
        { "rattletrap", "Clockwerk" },
        { "shredder", "Timbersaw" },
        { "skeleton_king", "Wraith King" },
        { "treant", "Treant Protector" },
        { "vengefulspirit", "Vengeful Spirit" },
        { "windrunner", "Windranger" },
        { "wisp", "Io" },
        { "zuus", "Zeus" },
        { "keeper_of_the_light", "Keeper of the Light" },
        { "abyssal_underlord", "Underlord" },
        { "elder_titan", "Elder Titan" },
        { "shadow_demon", "Shadow Demon" },
        { "shadow_shaman", "Shadow Shaman" },
        { "storm_spirit", "Storm Spirit" },
        { "ember_spirit", "Ember Spirit" },
        { "earth_spirit", "Earth Spirit" },
        { "void_spirit", "Void Spirit" },
        { "monkey_king", "Monkey King" },
        { "dragon_knight", "Dragon Knight" },
        { "crystal_maiden", "Crystal Maiden" },
        { "drow_ranger", "Drow Ranger" },
        { "phantom_assassin", "Phantom Assassin" },
        { "phantom_lancer", "Phantom Lancer" },
        { "templar_assassin", "Templar Assassin" },
        { "sand_king", "Sand King" },
        { "faceless_void", "Faceless Void" },
        { "night_stalker", "Night Stalker" },
        { "bounty_hunter", "Bounty Hunter" },
        { "legion_commander", "Legion Commander" },
        { "skywrath_mage", "Skywrath Mage" },
        { "dark_willow", "Dark Willow" },
        { "primal_beast", "Primal Beast" },
        { "arc_warden", "Arc Warden" },
        { "witch_doctor", "Witch Doctor" },
        { "death_prophet", "Death Prophet" },
        { "dark_seer", "Dark Seer" },
        { "lone_druid", "Lone Druid" },
        { "chaos_knight", "Chaos Knight" },
        { "naga_siren", "Naga Siren" },
        { "nyx_assassin", "Nyx Assassin" },
        { "ogre_magi", "Ogre Magi" },
        { "spirit_breaker", "Spirit Breaker" },
        { "troll_warlord", "Troll Warlord" },
        { "bristleback", "Bristleback" },
        { "ancient_apparition", "Ancient Apparition" },
    };

    private static string ToDisplay(string slug)
    {
        if (Exceptions.TryGetValue(slug, out var name)) return name;

        var parts = slug.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }
}