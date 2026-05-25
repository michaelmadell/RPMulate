using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RPMulate.Models;
using RPMulate.Services;

namespace RPMulate.ViewModels;

// ===================================================================
//  Relay command (simple ICommand)
// ===================================================================
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => _execute(p);
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => !_isExecuting && (_canExecute?.Invoke(p) ?? true);

    public async void Execute(object? p)
    {
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try { await _execute(p); }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

// ===================================================================
//  Drive slot view model
// ===================================================================
public class DriveSlotViewModel : INotifyPropertyChanged
{
    private readonly DriveService _driveService;
    private CancellationTokenSource? _cts;

    public DriveSlotViewModel(int index, DriveService driveService)
    {
        SlotIndex = index;
        _driveService = driveService;
        MediaType = DriveMediaType.CD_ROM;
        UpdateAvailableSpeeds();
    }

    // -- Backing fields ----------------------------------------------
    private int _slotIndex;
    private char _driveLetter = '\0';
    private DriveMediaType _mediaType;
    private DriveStatus _status = DriveStatus.Empty;
    private string? _imagePath;
    private long _imageSizeBytes;
    private SpeedProfile _selectedSpeed = SpeedProfiles.Max;
    private ObservableCollection<SpeedProfile> _availableSpeeds = new();
    private double _currentReadSpeed;
    private double _currentWriteSpeed;
    private long _bytesTransferred;
    private string _statusMessage = "Empty – drag an image or click Browse";

    // -- Properties --------------------------------------------------
    public int SlotIndex { get => _slotIndex; set => Set(ref _slotIndex, value); }
    public char DriveLetter { get => _driveLetter; set => Set(ref _driveLetter, value); }
    public string DriveLetterDisplay => DriveLetter == '\0' ? "-" : $"{DriveLetter}:";

    public DriveMediaType MediaType
    {
        get => _mediaType;
        set { if (Set(ref _mediaType, value)) UpdateAvailableSpeeds(); }
    }

    public DriveStatus Status { get => _status; set { Set(ref _status, value); OnPropertyChanged(nameof(IsMounted)); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(CanMount)); OnPropertyChanged(nameof(CanEject)); OnPropertyChanged(nameof(StatusIcon)); } }
    public string? ImagePath { get => _imagePath; set { Set(ref _imagePath, value); OnPropertyChanged(nameof(ImageFileName)); OnPropertyChanged(nameof(HasImage)); OnPropertyChanged(nameof(CanMount)); } }
    public long ImageSizeBytes { get => _imageSizeBytes; set { Set(ref _imageSizeBytes, value); OnPropertyChanged(nameof(ImageSizeFormatted)); } }
    public SpeedProfile SelectedSpeed { get => _selectedSpeed; set => Set(ref _selectedSpeed, value); }
    public ObservableCollection<SpeedProfile> AvailableSpeeds { get => _availableSpeeds; set => Set(ref _availableSpeeds, value); }
    public double CurrentReadSpeed { get => _currentReadSpeed; set => Set(ref _currentReadSpeed, value); }
    public double CurrentWriteSpeed { get => _currentWriteSpeed; set => Set(ref _currentWriteSpeed, value); }
    public long BytesTransferred { get => _bytesTransferred; set => Set(ref _bytesTransferred, value); }
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

    // -- Computed ----------------------------------------------------
    public string? ImageFileName => ImagePath is not null ? Path.GetFileName(ImagePath) : null;
    public bool HasImage => ImagePath is not null;
    public bool IsMounted => Status == DriveStatus.Mounted;
    public bool IsEmpty => Status == DriveStatus.Empty;
    public bool CanMount => HasImage && Status == DriveStatus.Empty;
    public bool CanEject => Status == DriveStatus.Mounted;

    public string StatusIcon => Status switch
    {
        DriveStatus.Empty => "\uE962",     // empty disc
        DriveStatus.Mounting => "\uE895",   // sync/loading
        DriveStatus.Mounted => "\uE73E",    // checkmark
        DriveStatus.Ejecting => "\uE7AD",   // eject
        DriveStatus.Error => "\uEA39",      // warning
        _ => "\uE962"
    };

    public string ImageSizeFormatted
    {
        get
        {
            if (ImageSizeBytes == 0) return "-";
            if (ImageSizeBytes >= 1L << 30) return $"{ImageSizeBytes / (double)(1L << 30):F2} GB";
            if (ImageSizeBytes >= 1L << 20) return $"{ImageSizeBytes / (double)(1L << 20):F2} MB";
            if (ImageSizeBytes >= 1L << 10) return $"{ImageSizeBytes / (double)(1L << 10):F1} KB";
            return $"{ImageSizeBytes} B";
        }
    }

    public string MediaTypeDisplay => MediaType switch
    {
        DriveMediaType.Floppy35DD => "3.5\" DD Floppy",
        DriveMediaType.Floppy35HD => "3.5\" HD Floppy",
        DriveMediaType.Floppy525DD => "5.25\" DD Floppy",
        DriveMediaType.Floppy525HD => "5.25\" HD Floppy",
        DriveMediaType.CD_ROM => "CD-ROM",
        DriveMediaType.DVD_ROM => "DVD-ROM",
        DriveMediaType.DVD_RW => "DVD±RW",
        DriveMediaType.BD_ROM => "Blu-ray",
        DriveMediaType.BD_RE => "BD-RE",
        _ => MediaType.ToString()
    };

    public Array MediaTypes => Enum.GetValues(typeof(DriveMediaType));

    // -- Commands ----------------------------------------------------
    private AsyncRelayCommand? _mountCommand;
    public ICommand MountCommand => _mountCommand ??= new AsyncRelayCommand(
        async _ => await MountAsync(),
        _ => CanMount);

    private AsyncRelayCommand? _ejectCommand;
    public ICommand EjectCommand => _ejectCommand ??= new AsyncRelayCommand(
        async _ => await EjectAsync(),
        _ => CanEject);

    private RelayCommand? _browseCommand;
    public ICommand BrowseCommand => _browseCommand ??= new RelayCommand(_ => BrowseForImage());

    private RelayCommand? _clearCommand;
    public ICommand ClearCommand => _clearCommand ??= new RelayCommand(
        _ => ClearImage(),
        _ => Status == DriveStatus.Empty && HasImage);

    // -- Logic -------------------------------------------------------
    public void SetImage(string path)
    {
        if (Status != DriveStatus.Empty) return;
        ImagePath = path;
        try
        {
            ImageSizeBytes = new FileInfo(path).Length;
            AutoDetectMediaType(path);
            StatusMessage = $"Ready to mount: {ImageFileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error reading file: {ex.Message}";
        }
    }

    private void ClearImage()
    {
        ImagePath = null;
        ImageSizeBytes = 0;
        StatusMessage = "Empty – drag an image or click Browse";
    }

    private void BrowseForImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a disc image",
            Filter = ImageFormats.BuildFileFilter(),
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
            SetImage(dlg.FileName);
    }

    private void AutoDetectMediaType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var size = ImageSizeBytes;

        // Floppy sizes
        if (ext is ".ima" or ".vfd" or ".flp" ||
            (ext == ".img" && size <= 2_949_120))
        {
            MediaType = size switch
            {
                <= 368_640 => DriveMediaType.Floppy525DD,
                <= 737_280 => DriveMediaType.Floppy35DD,
                <= 1_228_800 => DriveMediaType.Floppy525HD,
                _ => DriveMediaType.Floppy35HD
            };
            return;
        }

        // Optical – estimate from file size
        MediaType = size switch
        {
            <= 737_280_000L => DriveMediaType.CD_ROM,         // up to ~700 MB → CD
            <= 5_000_000_000L => DriveMediaType.DVD_ROM,      // up to ~4.7 GB → DVD
            _ => DriveMediaType.BD_ROM                         // >4.7 GB → Blu-ray
        };
    }

    private async Task MountAsync()
    {
        Status = DriveStatus.Mounting;
        StatusMessage = "Mounting image…";
        _cts = new CancellationTokenSource();

        try
        {
            var letter = await _driveService.MountImageAsync(
                ImagePath!, MediaType, SelectedSpeed, _cts.Token);
            DriveLetter = letter;
            Status = DriveStatus.Mounted;
            StatusMessage = $"Mounted on {DriveLetter}: ({SelectedSpeed.Name})";
        }
        catch (Exception ex)
        {
            Status = DriveStatus.Error;
            StatusMessage = $"Mount failed: {ex.Message}";
            // Auto-recover to empty after a delay
            await Task.Delay(4000);
            if (Status == DriveStatus.Error) Status = DriveStatus.Empty;
        }
    }

    private async Task EjectAsync()
    {
        Status = DriveStatus.Ejecting;
        StatusMessage = "Ejecting…";
        try
        {
            await _driveService.UnmountAsync(DriveLetter);
            DriveLetter = '\0';
            Status = DriveStatus.Empty;
            StatusMessage = HasImage
                ? $"Ejected. Re-mount {ImageFileName}?"
                : "Empty – drag an image or click Browse";
        }
        catch (Exception ex)
        {
            Status = DriveStatus.Error;
            StatusMessage = $"Eject failed: {ex.Message}";
        }
    }

    private void UpdateAvailableSpeeds()
    {
        var profiles = SpeedProfiles.GetProfilesForMedia(MediaType);
        AvailableSpeeds = new ObservableCollection<SpeedProfile>(profiles);
        SelectedSpeed = profiles.First(); // MAX by default
    }

    // -- INotifyPropertyChanged --------------------------------------
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

// ===================================================================
//  Main window view model
// ===================================================================
public class MainViewModel : INotifyPropertyChanged
{
    private readonly DriveService _driveService = new();

    public MainViewModel()
    {
        // Start with 1 drive slot1; user can add more
        for (int i = 0; i < 1; i++) {
            DriveSlots.Add(new DriveSlotViewModel(i, _driveService));
        };
    }

    public Version AppVersion
    {
        get;
    } = typeof(MainViewModel).Assembly.GetName().Version;


    public ObservableCollection<DriveSlotViewModel> DriveSlots { get; } = new();

    private DriveSlotViewModel? _selectedSlot;
    public DriveSlotViewModel? SelectedSlot
    {
        get => _selectedSlot;
        set { _selectedSlot = value; OnPropertyChanged(); }
    }

    // -- Commands ----------------------------------------------------
    private RelayCommand? _addSlotCommand;
    public ICommand AddSlotCommand => _addSlotCommand ??= new RelayCommand(
        _ =>
        {
            if (DriveSlots.Count >= 8) return; // cap at 8 virtual drives
            DriveSlots.Add(new DriveSlotViewModel(DriveSlots.Count, _driveService));
        },
        _ => DriveSlots.Count < 8);

    private RelayCommand? _removeSlotCommand;
    public ICommand RemoveSlotCommand => _removeSlotCommand ??= new RelayCommand(
        _ =>
        {
            if (SelectedSlot is not null && SelectedSlot.IsEmpty)
                DriveSlots.Remove(SelectedSlot);
        },
        _ => SelectedSlot is not null && SelectedSlot.IsEmpty && DriveSlots.Count > 1);

    private AsyncRelayCommand? _ejectAllCommand;
    public ICommand EjectAllCommand => _ejectAllCommand ??= new AsyncRelayCommand(
        async _ =>
        {
            foreach (var slot in DriveSlots.Where(s => s.CanEject))
                slot.EjectCommand.Execute(null);
        });

    // -- INotifyPropertyChanged --------------------------------------
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
