namespace RankUp.Models;

public class RankTier
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public string Background { get; set; } = "";
    public int MinTier { get; set; }
    public string Initials { get; set; } = "";

    public static List<RankTier> All => new()
    {
        new() { Name = "Herald",   Initials = "H",  Color = "#8B5A2B", Background = "#2A1A0A", MinTier = 10 },
        new() { Name = "Guardian", Initials = "G",  Color = "#7B8C99", Background = "#1A2028", MinTier = 20 },
        new() { Name = "Crusader", Initials = "C",  Color = "#C89B4A", Background = "#2A220A", MinTier = 30 },
        new() { Name = "Archon",   Initials = "A",  Color = "#4A8C5A", Background = "#0A2A15", MinTier = 40 },
        new() { Name = "Legend",   Initials = "L",  Color = "#4A7BC8", Background = "#0A1A2A", MinTier = 50 },
        new() { Name = "Ancient",  Initials = "A",  Color = "#7A4AC8", Background = "#1A0A2A", MinTier = 60 },
        new() { Name = "Divine",   Initials = "D",  Color = "#4AC8E0", Background = "#0A2028", MinTier = 70 },
        new() { Name = "Immortal", Initials = "I",  Color = "#C84A4A", Background = "#2A0A0A", MinTier = 80 }
    };

    public static RankTier FromTier(int rankTier)
    {
        if (rankTier < 10) return new RankTier { Name = "Без ранга", Color = "#4a5568", Background = "#232f38", Initials = "?" };
        return All.LastOrDefault(r => rankTier >= r.MinTier) ?? All[0];
    }
}