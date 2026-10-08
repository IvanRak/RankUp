namespace RankUp.Models;

public class FriendMatchView
{
    public DotaMatch Match { get; set; } = null!;
    public int FriendHeroId { get; set; }
    public int FriendKills { get; set; }
    public int FriendDeaths { get; set; }
    public int FriendAssists { get; set; }

    public bool IsWin => Match.IsWin;
    public string Result => Match.Result;
    public string DateLabel => Match.DateLabel;
    public string DurationLabel => Match.DurationLabel;

    public string FriendKda => $"{FriendKills} / {FriendDeaths} / {FriendAssists}";

    // 👇 ИСПРАВЛЕНО: через HeroDatabase — учитывает локальный кэш
    public string FriendHeroIcon => HeroDatabase.GetIconUrl(FriendHeroId);
}