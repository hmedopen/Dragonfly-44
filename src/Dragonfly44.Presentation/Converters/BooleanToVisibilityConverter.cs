using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Dragonfly44.Presentation.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool boolValue && boolValue;
        if (Inverse) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        bool isVisible = value is Visibility v && v == Visibility.Visible;
        return Inverse ? !isVisible : isVisible;
    }
}
