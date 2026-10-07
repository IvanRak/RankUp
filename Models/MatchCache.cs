using SQLite;

namespace RankUp.Models;

[Table("matches")]
public class MatchCache
{
    [PrimaryKey]
    public long MatchId { get; set; }

    public long AccountId { get; set; }
    public int HeroId { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Duration { get; set; }
    public long StartTime { get; set; }
    public bool IsWin { get; set; }
    public long CachedAt { get; set; }
    public string AlliesJson { get; set; } = "";

    // Новые поля
    public string MyItemsJson { get; set; } = "";
    public int MyPosition { get; set; }
}