namespace RankUp.Models;

public class MatchDetails
{
    public long MatchId { get; set; }
    public bool RadiantWin { get; set; }
    public int Duration { get; set; }
    public int GameMode { get; set; }
    public long StartTime { get; set; }
    public long MyAccountId { get; set; }
    public List<MatchPlayer> Radiant { get; set; } = new();
    public List<MatchPlayer> Dire { get; set; } = new();

    public string DurationLabel => TimeSpan.FromSeconds(Duration).ToString(@"mm\:ss");
    public string DateLabel => DateTimeOffset.FromUnixTimeSeconds(StartTime).LocalDateTime.ToString("dd.MM.yyyy HH:mm");

    public string ModeName => GameMode switch
    {
        1 => "All Pick",
        2 => "Captains Mode",
        3 => "Random Draft",
        4 => "Single Draft",
        5 => "All Random",
        22 => "Ranked All Pick",
        _ => $"Mode {GameMode}"
    };

    public bool IAmRadiant => Radiant.Any(p => p.AccountId == MyAccountId);
    public bool IWin => IAmRadiant == RadiantWin;
}

public class MatchPlayer
{
    public long AccountId { get; set; }
    public int PlayerSlot { get; set; }
    public int HeroId { get; set; }
    public string Name { get; set; } = "";
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int NetWorth { get; set; }
    public int LastHits { get; set; }
    public int HeroDamage { get; set; }

    // === Итемы ===
    public List<int> ItemIds { get; set; } = new();

    public bool IsRadiant => PlayerSlot < 128;
    public bool IsMe => false;

    public string Kda => $"{Kills} / {Deaths} / {Assists}";
    public string NetWorthLabel => $"{NetWorth / 1000.0:F1}k";
    public string HeroIcon => $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{HeroName}.png";
    public string HeroName => new DotaMatch { HeroId = HeroId }.HeroName;

    // === URL иконок предметов ===
    public List<string> ItemIcons
    {
        get
        {
            var result = new List<string>();
            foreach (var id in ItemIds.Take(6))
            {
                var url = ItemDatabase.GetIconUrl(id);
                if (!string.IsNullOrEmpty(url)) result.Add(url);
            }
            return result;
        }
    }
}