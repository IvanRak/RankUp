namespace RankUp.Models;

public class Teammate
{
    public long AccountId { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public int GamesTogether { get; set; }

    public long SteamId => AccountId + 76561197960265728L;
    public string GamesLabel => $"{GamesTogether} игр";
}