using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace YoutubeApp.ValueConverters;

public class LongFilesizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Utils.FormatBytes((long)value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}