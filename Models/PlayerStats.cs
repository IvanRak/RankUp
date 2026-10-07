namespace RankUp.Models;

public class PlayerStats
{
    public long AccountId { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int RankTier { get; set; }
    public int LeaderboardRank { get; set; }

    public int Total => Wins + Losses;
    public double WinRate => Total == 0 ? 0 : (double)Wins / Total * 100;

    public string RankName => RankTier switch
    {
        >= 80 => "Immortal",
        >= 70 => "Divine",
        >= 60 => "Ancient",
        >= 50 => "Legend",
        >= 40 => "Archon",
        >= 30 => "Crusader",
        >= 20 => "Guardian",
        >= 10 => "Herald",
        _ => "Без ранга"
    };

    public int RankStars => RankTier >= 10 && RankTier < 80 ? RankTier % 10 : 0;

    public string RankFullLabel => RankTier < 10
        ? "Без ранга"
        : RankTier >= 80
            ? LeaderboardRank > 0 ? $"Immortal #{LeaderboardRank}" : "Immortal"
            : $"{RankName} {RankStars}";

    public string RankColor => RankTier switch
    {
        >= 80 => "#C84A4A",
        >= 70 => "#4AC8E0",
        >= 60 => "#7A4AC8",
        >= 50 => "#4A7BC8",
        >= 40 => "#4A8C5A",
        >= 30 => "#C89B4A",
        >= 20 => "#7B8C99",
        >= 10 => "#8B5A2B",
        _ => "#4a5568"
    };
}