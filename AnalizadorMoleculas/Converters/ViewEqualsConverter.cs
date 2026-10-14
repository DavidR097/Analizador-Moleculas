using System;
using System.Globalization;
using System.Windows.Data;

namespace AnalizadorMoleculas.Converters
{
    public class ViewEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var current = value as string ?? string.Empty;
            var target = parameter as string ?? string.Empty;
            return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isChecked = false;
            if (value is bool b) isChecked = b;
            if (!isChecked) return Binding.DoNothing;
            return parameter as string ?? string.Empty;
        }
    }
}
