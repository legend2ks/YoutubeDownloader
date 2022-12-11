using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace YoutubeApp.ValueConverters;

public class GridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var val = (double)value;
        var gridLength = new GridLength(val);
        return gridLength;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var val = (GridLength)value;
        return val.Value;
    }
}