using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TreePaste.Converters;

/// <summary>
/// bool 値を <see cref="System.Windows.FontWeight"/> に変換するコンバーター。true の場合は太字、false の場合は通常の太さを返す。
/// Converter that maps a bool value to a <see cref="System.Windows.FontWeight"/>. Returns Bold for true, Normal for false.
/// </summary>
public class BoolToFontWeightConverter : IValueConverter
{
    /// <summary>
    /// bool 値を FontWeight に変換する。
    /// Converts a bool value to a FontWeight.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isDir && isDir ? FontWeights.Bold : FontWeights.Normal;
    }

    /// <summary>
    /// 逆変換はサポートされていない。
    /// Reverse conversion is not supported.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
