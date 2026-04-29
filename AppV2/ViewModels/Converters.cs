using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ASCOM.DeKoi.DeFocuserApp.ViewModels
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public Visibility TrueValue { get; set; } = Visibility.Visible;
        public Visibility FalseValue { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool v && v;
            if (parameter is string s && s == "Invert") b = !b;
            return b ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == TrueValue;
        }
    }

    public class BoolToInvertedBoolConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => !(v is bool b && b);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => !(v is bool b && b);
    }

    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Equals(value as string, parameter as string, StringComparison.Ordinal);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? parameter : Binding.DoNothing;
        }
    }

    public class IntEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int v = System.Convert.ToInt32(value);
            int p = System.Convert.ToInt32(parameter);
            return v == p;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? System.Convert.ToInt32(parameter) : Binding.DoNothing;
        }
    }

    public class LogKindToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogKind k)
            {
                switch (k)
                {
                    case LogKind.Send: return new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD));
                    case LogKind.Recv: return new SolidColorBrush(Color.FromRgb(0xCF, 0xD3, 0xDA));
                    case LogKind.Ok: return new SolidColorBrush(Color.FromRgb(0x7B, 0xE3, 0xB3));
                    case LogKind.Err: return new SolidColorBrush(Color.FromRgb(0xFF, 0x90, 0x94));
                    case LogKind.Warn: return new SolidColorBrush(Color.FromRgb(0xFF, 0xC6, 0x6E));
                    case LogKind.Info: return new SolidColorBrush(Color.FromRgb(0xAA, 0xB1, 0xBD));
                }
            }
            return new SolidColorBrush(Color.FromRgb(0xCF, 0xD3, 0xDA));
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                double pct = System.Convert.ToDouble(value);
                return pct.ToString("F1", culture) + "%";
            }
            catch { return "0.0%"; }
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public class WidthFromPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                double pct = System.Convert.ToDouble(value);
                double total = parameter == null ? 100.0 : System.Convert.ToDouble(parameter);
                return Math.Max(0, Math.Min(total, total * pct / 100.0));
            }
            catch { return 0.0; }
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public class ThousandsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try { return System.Convert.ToInt64(value).ToString("N0", culture); }
            catch { return value?.ToString() ?? string.Empty; }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0;
            string s = value.ToString();
            string clean = s.Replace(",", "").Replace(" ", "").Replace(" ", "");
            if (int.TryParse(clean, NumberStyles.Integer, culture, out int n)) return n;
            return Binding.DoNothing;
        }
    }

    public class NullableDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && !double.IsNaN(d)) return d;
            return DependencyProperty.UnsetValue;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v;
    }

    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    var color = (Color)ColorConverter.ConvertFromString(s);
                    return new SolidColorBrush(color);
                }
            }
            catch { }
            return new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush b)
            {
                return b.Color.ToString();
            }
            return Binding.DoNothing;
        }
    }
}
