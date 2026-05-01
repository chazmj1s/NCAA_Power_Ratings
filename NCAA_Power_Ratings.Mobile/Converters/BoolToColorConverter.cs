using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace NCAA_Power_Ratings.Mobile.Converters
{
    /// <summary>
    /// Converts boolean to color for Top 25 highlighting
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isTop25 && isTop25)
            {
                // Gold color for Top 25
                return Color.FromArgb("#BF5700");
            }

            // Gray for others
            return Color.FromArgb("#808080");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
