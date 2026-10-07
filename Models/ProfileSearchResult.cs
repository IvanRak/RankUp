namespace RankUp.Models;

public class ProfileSearchResult
{
    public long AccountId { get; set; }
    public long SteamId { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
}