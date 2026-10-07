using System.Globalization;

namespace RankUp.Converters;

public class WinColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isWin && isWin ? Color.FromArgb("#4ade80") : Color.FromArgb("#f87171");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}