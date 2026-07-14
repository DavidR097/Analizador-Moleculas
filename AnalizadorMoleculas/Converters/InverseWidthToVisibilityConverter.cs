using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AnalizadorMoleculas.Converters
{
    public class InverseWidthToVisibilityConverter : IValueConverter
    {
        public double Threshold { get; set; } = 120;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return d < Threshold ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
