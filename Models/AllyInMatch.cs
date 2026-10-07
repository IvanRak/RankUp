namespace RankUp.Models;

public class AllyInMatch
{
    public long AccountId { get; set; }
    public int HeroId { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
}