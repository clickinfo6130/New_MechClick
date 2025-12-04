using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ExcelToPostgres.Converters
{
    // 현재 사용하지 않음 - 추후 색상 표시 기능 추가 시 사용
    public class ColorCodeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var colorCode = value as string;
            if (string.IsNullOrEmpty(colorCode))
                colorCode = "#808080";

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorCode);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return string.Format("#{0:X2}{1:X2}{2:X2}", brush.Color.R, brush.Color.G, brush.Color.B);
            }
            return "#808080";
        }
    }

    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "예" : "아니오";
            return "아니오";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return str == "예" || (str != null && str.ToUpper() == "TRUE");
        }
    }
}
