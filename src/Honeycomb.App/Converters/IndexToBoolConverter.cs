using Avalonia.Data.Converters;
using System.Globalization;

namespace Honeycomb.App.Converters;

/// <summary>True when the bound integer equals the converter parameter (radio-group index selection).</summary>
public class IndexToBoolConverter : IValueConverter
{
    public static readonly IndexToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int index && parameter is string p && int.TryParse(p, out int expected) && index == expected;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is string p && int.TryParse(p, out int expected)
            ? expected
            : Avalonia.Data.BindingOperations.DoNothing;
}
