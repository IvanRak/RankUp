using System.Globalization;

namespace RankUp.Converters;

public class RankToMedalConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int tier) return "rank_herald.png";

        return tier switch
        {
            >= 80 => "rank_immortal.png",
            >= 70 => "rank_divine.png",
            >= 60 => "rank_ancient.png",
            >= 50 => "rank_legend.png",
            >= 40 => "rank_archon.png",
            >= 30 => "rank_crusader.png",
            >= 20 => "rank_guardian.png",
            >= 10 => "rank_herald.png",
            _ => "rank_none.png"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}