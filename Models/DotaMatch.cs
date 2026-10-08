namespace RankUp.Models;

public class DotaMatch
{
    public long MatchId { get; set; }
    public int HeroId { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Duration { get; set; }
    public long StartTime { get; set; }
    public bool IsWin { get; set; }
    public bool IsFavorite { get; set; }
    public List<long> TeammateIds { get; set; } = new();
    public List<AllyInMatch> Allies { get; set; } = new();

    public string MyItemsJson { get; set; } = "";
    public int MyPosition { get; set; } = 0;

    public string Result => IsWin ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
    public string Kda => $"{Kills} / {Deaths} / {Assists}";
    public string DurationLabel => TimeSpan.FromSeconds(Duration).ToString(@"mm\:ss");
    public string DateLabel => DateTimeOffset.FromUnixTimeSeconds(StartTime).LocalDateTime.ToString("dd.MM HH:mm");

    public string HeroIcon => HeroDatabase.GetIconUrl(HeroId);
    public string HeroName => HeroDatabase.GetSlug(HeroId);
    public string PositionLabel => MyPosition > 0 ? $"П{MyPosition}" : "";

    public List<string> MyItemIcons
    {
        get
        {
            if (string.IsNullOrEmpty(MyItemsJson)) return new List<string>();

            return MyItemsJson
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0)
                .Select(ItemDatabase.GetIconUrl)
                .Where(url => !string.IsNullOrEmpty(url))
                .Take(6)
                .ToList();
        }
    }
}