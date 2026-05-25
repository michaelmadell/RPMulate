namespace RPMulate.Converters
{
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RPMulate.Models;

    public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Collapsed;
}

public class DriveStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        return value is DriveStatus status ? status switch
        {
            DriveStatus.Empty    => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
            DriveStatus.Mounting => new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)),
            DriveStatus.Mounted  => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
            DriveStatus.Ejecting => new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)),
            DriveStatus.Error    => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
        } : Binding.DoNothing;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public class DriveStatusToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        return value is DriveStatus status ? status switch
        {
            DriveStatus.Mounted  => new SolidColorBrush(Color.FromArgb(0x18, 0x22, 0xC5, 0x5E)),
            DriveStatus.Error    => new SolidColorBrush(Color.FromArgb(0x18, 0xEF, 0x44, 0x44)),
            DriveStatus.Mounting => new SolidColorBrush(Color.FromArgb(0x10, 0xF5, 0xA6, 0x23)),
            _ => new SolidColorBrush(Colors.Transparent)
        } : Binding.DoNothing;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public class MediaTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        return value is DriveMediaType media ? media switch
        {
            DriveMediaType.Floppy35DD or DriveMediaType.Floppy35HD or
            DriveMediaType.Floppy525DD or DriveMediaType.Floppy525HD
                => "\uE967",  // floppy icon
            DriveMediaType.CD_ROM
                => "\uE958",  // CD icon
            DriveMediaType.DVD_ROM or DriveMediaType.DVD_RW
                => "\uE958",  // DVD (same family)
            DriveMediaType.BD_ROM or DriveMediaType.BD_RE
                => "\uE958",  // BD (same family)
            _ => "\uE964"
        } : "";
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => Binding.DoNothing;
}

public class MediaTypeToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        return value is DriveMediaType media ? media switch
        {
            DriveMediaType.Floppy35DD  => "3.5\" DD Floppy",
            DriveMediaType.Floppy35HD  => "3.5\" HD Floppy",
            DriveMediaType.Floppy525DD => "5.25\" DD Floppy",
            DriveMediaType.Floppy525HD => "5.25\" HD Floppy",
            DriveMediaType.CD_ROM      => "CD-ROM",
            DriveMediaType.DVD_ROM     => "DVD-ROM",
            DriveMediaType.DVD_RW      => "DVD±RW",
            DriveMediaType.BD_ROM      => "Blu-ray ROM",
            DriveMediaType.BD_RE       => "BD-RE",
            _ => media.ToString()
        } : "";
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
}
