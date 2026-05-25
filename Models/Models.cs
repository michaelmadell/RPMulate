using System;
using System.Collections.Generic;

namespace RPMulate.Models;

// -- Drive type definitions ------------------------------------------
public enum DriveMediaType
{
    Floppy35DD,    // 3.5" DD  – 720 KB
    Floppy35HD,    // 3.5" HD  – 1.44 MB
    Floppy525DD,   // 5.25" DD – 360 KB
    Floppy525HD,   // 5.25" HD – 1.2 MB
    CD_ROM,        // CD-ROM   – up to 700 MB
    DVD_ROM,       // DVD-ROM  – up to 4.7 GB / 8.5 GB DL
    DVD_RW,        // DVD±RW
    BD_ROM,        // Blu-ray  – up to 25 / 50 GB
    BD_RE,         // BD-RE rewritable
}

public enum DriveStatus
{
    Empty,
    Mounting,
    Mounted,
    Ejecting,
    Error
}

// -- Realistic speed profiles ----------------------------------------
public class SpeedProfile
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public long ReadBytesPerSecond { get; init; }
    public long WriteBytesPerSecond { get; init; }
    public int SeekTimeMs { get; init; }
    public bool IsMaxSpeed { get; init; }

    public string ReadSpeedFormatted => IsMaxSpeed ? "MAX" : FormatSpeed(ReadBytesPerSecond);
    public string WriteSpeedFormatted => IsMaxSpeed ? "MAX" : FormatSpeed(WriteBytesPerSecond);

    private static string FormatSpeed(long bytesPerSec)
    {
        if (bytesPerSec >= 1_000_000_000) return $"{bytesPerSec / 1_000_000_000.0:F1} GB/s";
        if (bytesPerSec >= 1_000_000) return $"{bytesPerSec / 1_000_000.0:F1} MB/s";
        if (bytesPerSec >= 1_000) return $"{bytesPerSec / 1_000.0:F1} KB/s";
        return $"{bytesPerSec} B/s";
    }
}

// -- Speed profile catalogue -----------------------------------------
public static class SpeedProfiles
{
    public static readonly SpeedProfile Max = new()
    {
        Name = "MAX",
        Description = "No speed throttling – transfers at full system speed",
        ReadBytesPerSecond = 0,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 0,
        IsMaxSpeed = true
    };

    // -- Floppy --
    public static readonly SpeedProfile Floppy35DD = new()
    {
        Name = "3.5\" DD (500 Kbps)",
        Description = "Double-density 3.5\" floppy – 500 Kbit/s raw, ~31 KB/s usable",
        ReadBytesPerSecond = 31_250,
        WriteBytesPerSecond = 31_250,
        SeekTimeMs = 94
    };

    public static readonly SpeedProfile Floppy35HD = new()
    {
        Name = "3.5\" HD (1 Mbps)",
        Description = "High-density 3.5\" floppy – 1 Mbit/s raw, ~62 KB/s usable",
        ReadBytesPerSecond = 62_500,
        WriteBytesPerSecond = 62_500,
        SeekTimeMs = 94
    };

    public static readonly SpeedProfile Floppy525DD = new()
    {
        Name = "5.25\" DD (250 Kbps)",
        Description = "Double-density 5.25\" floppy – 250 Kbit/s raw, ~15 KB/s usable",
        ReadBytesPerSecond = 15_625,
        WriteBytesPerSecond = 15_625,
        SeekTimeMs = 105
    };

    public static readonly SpeedProfile Floppy525HD = new()
    {
        Name = "5.25\" HD (500 Kbps)",
        Description = "High-density 5.25\" floppy – 500 Kbit/s raw, ~31 KB/s usable",
        ReadBytesPerSecond = 31_250,
        WriteBytesPerSecond = 31_250,
        SeekTimeMs = 105
    };

    // -- CD speeds (1× = 150 KB/s = 153,600 B/s) --
    public static readonly SpeedProfile CD_1x = new()
    {
        Name = "CD 1× (150 KB/s)",
        Description = "Original CD-ROM speed – 150 KB/s, typical of early drives (1984–1992)",
        ReadBytesPerSecond = 153_600,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 400
    };

    public static readonly SpeedProfile CD_4x = new()
    {
        Name = "CD 4× (600 KB/s)",
        Description = "Quad-speed CD-ROM – common in mid-1990s PCs",
        ReadBytesPerSecond = 614_400,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 200
    };

    public static readonly SpeedProfile CD_8x = new()
    {
        Name = "CD 8× (1.2 MB/s)",
        Description = "8× CD-ROM – late 1990s standard",
        ReadBytesPerSecond = 1_228_800,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 150
    };

    public static readonly SpeedProfile CD_24x = new()
    {
        Name = "CD 24× (3.6 MB/s)",
        Description = "24× CD-ROM – common in early 2000s",
        ReadBytesPerSecond = 3_686_400,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 110
    };

    public static readonly SpeedProfile CD_52x = new()
    {
        Name = "CD 52× (7.8 MB/s)",
        Description = "Maximum CD-ROM speed – theoretical peak, most drives hit ~40× sustained",
        ReadBytesPerSecond = 7_987_200,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 85
    };

    public static readonly SpeedProfile CD_RW_4x = new()
    {
        Name = "CD-RW 4× Write (600 KB/s)",
        Description = "Rewritable CD – 4× write speed",
        ReadBytesPerSecond = 3_686_400,
        WriteBytesPerSecond = 614_400,
        SeekTimeMs = 150
    };

    // -- DVD speeds (1× = 1.385 MB/s = 1,353,600 B/s) --
    public static readonly SpeedProfile DVD_1x = new()
    {
        Name = "DVD 1× (1.39 MB/s)",
        Description = "Base DVD speed – used in early DVD-ROM drives and standalone players",
        ReadBytesPerSecond = 1_385_000,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 130
    };

    public static readonly SpeedProfile DVD_4x = new()
    {
        Name = "DVD 4× (5.5 MB/s)",
        Description = "4× DVD-ROM – typical early-2000s drive",
        ReadBytesPerSecond = 5_540_000,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 110
    };

    public static readonly SpeedProfile DVD_8x = new()
    {
        Name = "DVD 8× (11.1 MB/s)",
        Description = "8× DVD – common burn speed for DVD±R media",
        ReadBytesPerSecond = 11_080_000,
        WriteBytesPerSecond = 11_080_000,
        SeekTimeMs = 90
    };

    public static readonly SpeedProfile DVD_16x = new()
    {
        Name = "DVD 16× (22.2 MB/s)",
        Description = "Maximum practical DVD speed – peak read speed in modern drives",
        ReadBytesPerSecond = 22_160_000,
        WriteBytesPerSecond = 22_160_000,
        SeekTimeMs = 75
    };

    public static readonly SpeedProfile DVD_RW_6x = new()
    {
        Name = "DVD-RW 6× Write (8.3 MB/s)",
        Description = "Rewritable DVD at 6× – common write speed for DVD±RW media",
        ReadBytesPerSecond = 11_080_000,
        WriteBytesPerSecond = 8_310_000,
        SeekTimeMs = 100
    };

    // -- Blu-ray speeds (1× = 4.5 MB/s = 4,500,000 B/s) --
    public static readonly SpeedProfile BD_1x = new()
    {
        Name = "BD 1× (4.5 MB/s)",
        Description = "Base Blu-ray speed – used for movie playback",
        ReadBytesPerSecond = 4_500_000,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 180
    };

    public static readonly SpeedProfile BD_2x = new()
    {
        Name = "BD 2× (9 MB/s)",
        Description = "2× Blu-ray – early BD-ROM drives",
        ReadBytesPerSecond = 9_000_000,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 150
    };

    public static readonly SpeedProfile BD_6x = new()
    {
        Name = "BD 6× (27 MB/s)",
        Description = "6× Blu-ray – mid-range BD reader/writer speed",
        ReadBytesPerSecond = 27_000_000,
        WriteBytesPerSecond = 27_000_000,
        SeekTimeMs = 100
    };

    public static readonly SpeedProfile BD_12x = new()
    {
        Name = "BD 12× (54 MB/s)",
        Description = "12× Blu-ray – high-end BD-ROM read speed",
        ReadBytesPerSecond = 54_000_000,
        WriteBytesPerSecond = 36_000_000,
        SeekTimeMs = 75
    };

    public static readonly SpeedProfile BD_16x = new()
    {
        Name = "BD 16× (72 MB/s)",
        Description = "Maximum Blu-ray speed – peak available in enthusiast drives",
        ReadBytesPerSecond = 72_000_000,
        WriteBytesPerSecond = 0,
        SeekTimeMs = 60
    };

    public static readonly SpeedProfile BD_RE_2x = new()
    {
        Name = "BD-RE 2× Write (9 MB/s)",
        Description = "Rewritable Blu-ray at 2× – standard BD-RE burn speed",
        ReadBytesPerSecond = 9_000_000,
        WriteBytesPerSecond = 9_000_000,
        SeekTimeMs = 150
    };

    // -- Lookup by media type ----------------------------------------
    public static List<SpeedProfile> GetProfilesForMedia(DriveMediaType mediaType) => mediaType switch
    {
        DriveMediaType.Floppy35DD => new() { Max, Floppy35DD },
        DriveMediaType.Floppy35HD => new() { Max, Floppy35HD, Floppy35DD },
        DriveMediaType.Floppy525DD => new() { Max, Floppy525DD },
        DriveMediaType.Floppy525HD => new() { Max, Floppy525HD, Floppy525DD },
        DriveMediaType.CD_ROM => new() { Max, CD_52x, CD_24x, CD_8x, CD_4x, CD_1x },
        DriveMediaType.DVD_ROM => new() { Max, DVD_16x, DVD_8x, DVD_4x, DVD_1x },
        DriveMediaType.DVD_RW => new() { Max, DVD_16x, DVD_RW_6x, DVD_8x, DVD_4x, DVD_1x },
        DriveMediaType.BD_ROM => new() { Max, BD_16x, BD_12x, BD_6x, BD_2x, BD_1x },
        DriveMediaType.BD_RE => new() { Max, BD_12x, BD_RE_2x, BD_6x, BD_2x, BD_1x },
        _ => new() { Max }
    };
}

// -- Supported image file formats ------------------------------------
public static class ImageFormats
{
    public record ImageFormat(string Extension, string Description, DriveMediaType[] CompatibleMedia);

    public static readonly ImageFormat[] Supported = new[]
    {
        new ImageFormat(".iso",  "ISO 9660 Disc Image",              new[] { DriveMediaType.CD_ROM, DriveMediaType.DVD_ROM, DriveMediaType.BD_ROM }),
        new ImageFormat(".img",  "Raw Disk Image",                   new[] { DriveMediaType.Floppy35HD, DriveMediaType.Floppy35DD, DriveMediaType.Floppy525HD, DriveMediaType.Floppy525DD, DriveMediaType.CD_ROM }),
        new ImageFormat(".ima",  "Floppy Disk Image",                new[] { DriveMediaType.Floppy35HD, DriveMediaType.Floppy35DD, DriveMediaType.Floppy525HD, DriveMediaType.Floppy525DD }),
        new ImageFormat(".vfd",  "Virtual Floppy Disk",              new[] { DriveMediaType.Floppy35HD, DriveMediaType.Floppy35DD }),
        new ImageFormat(".flp",  "Floppy Image",                     new[] { DriveMediaType.Floppy35HD, DriveMediaType.Floppy35DD }),
        new ImageFormat(".bin",  "Raw Binary Image",                 new[] { DriveMediaType.CD_ROM, DriveMediaType.DVD_ROM, DriveMediaType.BD_ROM }),
        new ImageFormat(".cue",  "Cue Sheet + Binary",               new[] { DriveMediaType.CD_ROM, DriveMediaType.DVD_ROM }),
        new ImageFormat(".nrg",  "Nero Burning ROM Image",           new[] { DriveMediaType.CD_ROM, DriveMediaType.DVD_ROM }),
        new ImageFormat(".mds",  "Media Descriptor + Data (.mdf)",   new[] { DriveMediaType.CD_ROM, DriveMediaType.DVD_ROM, DriveMediaType.BD_ROM }),
        new ImageFormat(".ccd",  "CloneCD Control File",             new[] { DriveMediaType.CD_ROM, DriveMediaType.DVD_ROM }),
        new ImageFormat(".udf",  "Universal Disc Format",            new[] { DriveMediaType.DVD_ROM, DriveMediaType.BD_ROM }),
    };

    public static string BuildFileFilter()
    {
        var allExts = string.Join(";", Array.ConvertAll(Supported, f => $"*{f.Extension}"));
        var filter = $"All Disc Images ({allExts})|{allExts}";
        foreach (var fmt in Supported)
            filter += $"|{fmt.Description} (*{fmt.Extension})|*{fmt.Extension}";
        return filter;
    }
}

// -- Virtual drive slot ----------------------------------------------
public class VirtualDrive
{
    public int SlotIndex { get; init; }
    public char DriveLetter { get; set; } = '\0';
    public DriveMediaType MediaType { get; set; } = DriveMediaType.CD_ROM;
    public DriveStatus Status { get; set; } = DriveStatus.Empty;
    public string? ImagePath { get; set; }
    public string? ImageFileName => ImagePath != null ? System.IO.Path.GetFileName(ImagePath) : null;
    public long ImageSizeBytes { get; set; }
    public SpeedProfile SpeedProfile { get; set; } = SpeedProfiles.Max;

    // Throughput stats (live, updated by the throttle service)
    public long BytesRead { get; set; }
    public long BytesWritten { get; set; }
    public double CurrentReadSpeed { get; set; }
    public double CurrentWriteSpeed { get; set; }
}
