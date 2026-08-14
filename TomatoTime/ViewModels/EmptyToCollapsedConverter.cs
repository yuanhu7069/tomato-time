using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TomatoTime.ViewModels;

/// <summary>统计图表空状态:Series 值数量为 0 时显示占位文案。</summary>
public class EmptyToCollapsedConverter : IValueConverter
{
    public static readonly EmptyToCollapsedConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // value 为 Series[0].Values.Count(int) 或 null
        if (value is int count) return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
