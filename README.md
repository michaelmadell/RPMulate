# RPMulate

*Mount disk images. Emulate drive behavior.*

A Windows desktop application (WPF / .NET 8) that lets you mount disc images as virtual drives with **realistic speed emulation** for CD, DVD, Blu-ray, and floppy media.

---

## Features

| Feature | Detail |
|---|---|
| **Multi-format support** | ISO, IMG, BIN/CUE, NRG, MDS, CCD, UDF, VFD, FLP, IMA |
| **Media type emulation** | 3.5"/5.25" floppy (DD/HD), CD-ROM, DVD-ROM, DVD±RW, Blu-ray, BD-RE |
| **Realistic speed profiles** | Authentic read/write speeds based on real hardware specs |
| **MAX speed mode** | Bypass throttling for full system-speed transfers |
| **Up to 8 virtual drives** | Add/remove drive slots on the fly |
| **Drag & drop** | Drop image files onto drive slots or the window |
| **Auto-detection** | Automatically identifies media type from file size/extension |
| **Dark UI** | Modern dark theme with status indicators |

---

## Speed Profiles

All speed values are based on real-world hardware specifications:

### Floppy
| Profile | Read/Write | Seek Time |
|---|---|---|
| 3.5" DD (500 Kbps) | ~31 KB/s | 94 ms |
| 3.5" HD (1 Mbps) | ~62 KB/s | 94 ms |
| 5.25" DD (250 Kbps) | ~15 KB/s | 105 ms |
| 5.25" HD (500 Kbps) | ~31 KB/s | 105 ms |

### CD-ROM (1× = 150 KB/s)
| Profile | Read Speed | Seek Time |
|---|---|---|
| CD 1× | 150 KB/s | 400 ms |
| CD 4× | 600 KB/s | 200 ms |
| CD 8× | 1.2 MB/s | 150 ms |
| CD 24× | 3.6 MB/s | 110 ms |
| CD 52× | 7.8 MB/s | 85 ms |

### DVD (1× = 1.385 MB/s)
| Profile | Read Speed | Seek Time |
|---|---|---|
| DVD 1× | 1.39 MB/s | 130 ms |
| DVD 4× | 5.5 MB/s | 110 ms |
| DVD 8× | 11.1 MB/s | 90 ms |
| DVD 16× | 22.2 MB/s | 75 ms |

### Blu-ray (1× = 4.5 MB/s)
| Profile | Read Speed | Seek Time |
|---|---|---|
| BD 1× | 4.5 MB/s | 180 ms |
| BD 2× | 9 MB/s | 150 ms |
| BD 6× | 27 MB/s | 100 ms |
| BD 12× | 54 MB/s | 75 ms |
| BD 16× | 72 MB/s | 60 ms |

---

## Architecture

```
RPMulate/
├-- Models/
│   └-- Models.cs            # DriveMediaType, SpeedProfile, ImageFormats, VirtualDrive
├-- ViewModels/
│   └-- MainViewModel.cs     # MainViewModel + DriveSlotViewModel (MVVM)
├-- Views/
│   ├-- MainWindow.xaml       # Full UI layout with drag-and-drop
│   └-- MainWindow.xaml.cs    # Code-behind (D&D handlers)
├-- Services/
│   └-- DriveService.cs       # Mount/unmount logic + ThrottledStream
├-- Converters/
│   └-- Converters.cs         # XAML value converters
├-- App.xaml                  # Global resources, theme, styles
├-- App.xaml.cs
└-- VirtualDriveManager.csproj
```

**Design pattern:** MVVM with `INotifyPropertyChanged`, `ICommand`, and data-bound XAML.

---

## Mount Backends

The app tries mount strategies in this order:

1. **Windows PowerShell `Mount-DiskImage`** - Built-in ISO/VHD mounting on Windows 8+
2. **ImDisk Virtual Disk Driver** - Full format support if [ImDisk](https://sourceforge.net/projects/imdisk-toolkit/) is installed
3. **Windows Virtual Disk API** - Native VHD/VHDX mounting via `virtdisk.dll`

For the broadest format support (BIN/CUE, NRG, MDS, floppy images), install **ImDisk Toolkit**.

---

## Building & Running

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- Windows 10/11

### Build
```bash
cd RPMulate
dotnet restore
dotnet build --configuration Release
```

### Run
```bash
dotnet run
```

### Publish (single-file EXE)
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

---

## Usage

1. **Launch** RPMulate
2. **Load an image**: Click **Browse** or drag-and-drop a disc image onto a drive slot
3. The app auto-detects media type (floppy/CD/DVD/BD) from the file size and extension
4. **Select speed**: Choose a realistic speed profile or **MAX** for unthrottled access
5. Click **Mount** to create the virtual drive
6. The assigned drive letter appears in Explorer
7. Click **Eject** when done

### Tips
- Drag multiple files at once - each gets its own slot
- Click **+ Add Drive** for up to 8 simultaneous virtual drives
- **Eject All** unmounts every active drive instantly

---

## Extending

### Adding new speed profiles
Add entries to `SpeedProfiles` in `Models.cs` and register them in `GetProfilesForMedia()`.

### Adding new image formats
Add entries to `ImageFormats.Supported` and update `AutoDetectMediaType()` in `DriveSlotViewModel`.

### Custom mount backend
Implement new mount logic in `DriveService.MountImageAsync()` - the strategy pattern makes it straightforward to add backends like WinCDEmu, Alcohol 120%, or a custom SCSI miniport.

---

## License

MIT - use freely for personal or commercial projects.
