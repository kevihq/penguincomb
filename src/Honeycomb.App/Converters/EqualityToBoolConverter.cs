using Avalonia.Data.Converters;
using System.Globalization;

namespace Honeycomb.App.Converters;

/// <summary>True when the bound value equals the converter parameter (radio-group selection).</summary>
public class EqualityToBoolConverter : IValueConverter
{
    public static readonly EqualityToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value?.ToString(), parameter?.ToString());

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter?.ToString() : Avalonia.Data.BindingOperations.DoNothing;
}
