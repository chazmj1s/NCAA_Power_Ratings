using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace NCAA_Power_Ratings.Mobile.Converters
{
    /// <summary>
    /// Returns a subtle alternating background color for odd-numbered list rows.
    /// </summary>
    public class OddRowBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOdd && isOdd)
                return AppInfo.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A")   // dark: very subtle dark stripe
                    : Color.FromArgb("#FFF8F0");  // light: barely-there warm tint

            return AppInfo.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1A1A1A")       // dark: default row
                : Colors.White;                   // light: clean white
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
