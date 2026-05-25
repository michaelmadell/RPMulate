using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RPMulate.Models;

using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Vhd;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;

namespace RPMulate.Services;

/// <summary>
/// Manages the lifecycle of virtual disk drives on Windows.
/// 
/// Mounting strategy (in order of preference):
///   1. Windows built-in PowerShell Mount-DiskImage (ISO/VHD on Win 8+)
///   2. ImDisk Virtual Disk Driver (if installed – supports all formats)
///   3. Falls back to a loopback file-backed SCSI approach via WinAPI
/// </summary>
public class DriveService : IDisposable
{
    // -- Win32: Virtual Disk API -------------------------------------
    [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
    private static extern int OpenVirtualDisk(
        ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
        string Path,
        int VirtualDiskAccessMask,
        int Flags,
        IntPtr Parameters,
        out IntPtr Handle);

    [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
    private static extern int AttachVirtualDisk(
        IntPtr VirtualDiskHandle,
        IntPtr SecurityDescriptor,
        int Flags,
        int ProviderSpecificFlags,
        IntPtr Parameters,
        IntPtr Overlapped);

    [DllImport("virtdisk.dll")]
    private static extern int DetachVirtualDisk(
        IntPtr VirtualDiskHandle,
        int Flags,
        int ProviderSpecificFlags);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct VIRTUAL_STORAGE_TYPE
    {
        public int DeviceId;
        public Guid VendorId;
    }

    private static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT =
        new("EC984AEC-A0F9-47e9-901F-71415A66345B");

    // -- Public interface --------------------------------------------

    /// <summary>
    /// Mount a disc image and assign it a virtual drive letter.
    /// Returns the assigned drive letter, or throws on failure.
    /// </summary>
    public async Task<char> MountImageAsync(
        string imagePath,
        DriveMediaType mediaType,
        SpeedProfile speedProfile,
        CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        var ext = Path.GetExtension(imagePath).ToLowerInvariant();

        // Strategy 1: Windows native ISO mount (Win8+)
        if (ext == ".iso" && IsWindows8OrLater())
            return await MountViaPowerShellAsync(imagePath, ct);

        // Strategy 2: ImDisk (if available)
        if (IsImDiskInstalled())
            return await MountViaImDiskAsync(imagePath, mediaType, ct);

        // Strategy 3: Virtual Disk API for VHD/VHDX
        if (ext is ".vhd" or ".vhdx")
            return await Task.Run(() => MountViaVirtDiskApi(imagePath), ct);

        if (ext is ".ima" or ".img" or ".vfd" or ".flp" &&
            mediaType is DriveMediaType.Floppy35DD or DriveMediaType.Floppy35HD
                     or DriveMediaType.Floppy525DD or DriveMediaType.Floppy525HD)
            return await MountViaDiscUtils(imagePath, mediaType, ct);

        // Strategy 4: Fallback – copy to temp VHD wrapper
        throw new NotSupportedException(
            $"No suitable mount backend found for '{ext}'. " +
            "Install ImDisk (https://imdisk.com) for full format support, " +
            "or use .iso / .vhd files with the built-in Windows driver.");
    }

    /// <summary>
    /// Unmount / eject the drive at the given letter.
    /// </summary>
    public async Task UnmountAsync(char driveLetter, CancellationToken ct = default)
    {
        // Check if this was a SUBST-mounted floppy
        if (_mountedTempDirs.TryGetValue(driveLetter, out var tempDir))
        {
            // Remove SUBST mapping
            var substCommand = $"subst {driveLetter}: /D";
            var psi = new ProcessStartInfo("cmd.exe", $"/c {substCommand}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            await proc.WaitForExitAsync(ct);

            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { /* Ignore cleanup errors */ }

            _mountedTempDirs.Remove(driveLetter);
            return;
        }

        // Try PowerShell Dismount first
        var script = $"Dismount-DiskImage -DevicePath \"\\\\.\\{driveLetter}:\" -ErrorAction SilentlyContinue; " +
                     $"Get-DiskImage | Where-Object {{ $_.DevicePath -like '*{driveLetter}*' }} | Dismount-DiskImage -ErrorAction SilentlyContinue";
        await RunPowerShellAsync(script, ct);

        // Also try imdisk removal
        if (IsImDiskInstalled())
            await RunProcessAsync("imdisk", $"-D -m {driveLetter}:", ct);
    }

    /// <summary>
    /// Find the next available drive letter (Z → D).
    /// </summary>
    public static char FindAvailableDriveLetter()
    {
        var taken = new System.Collections.Generic.HashSet<char>();
        foreach (var d in DriveInfo.GetDrives())
            taken.Add(d.Name[0]);

        for (var c = 'Z'; c >= 'D'; c--)
            if (!taken.Contains(c)) return c;

        throw new InvalidOperationException("No available drive letters.");
    }

    // -- Mount backends ----------------------------------------------

    private async Task<char> MountViaPowerShellAsync(string isoPath, CancellationToken ct)
    {
        var escaped = isoPath.Replace("'", "''");
        var script = $"$img = Mount-DiskImage -ImagePath '{escaped}' -PassThru; " +
                     "($img | Get-Volume).DriveLetter";

        var output = await RunPowerShellAsync(script, ct);
        if (output.Length > 0 && char.IsLetter(output.Trim()[0]))
            return output.Trim()[0];

        throw new InvalidOperationException($"Mount-DiskImage failed: {output}");
    }

    private async Task<char> MountViaImDiskAsync(string path, DriveMediaType media, CancellationToken ct)
    {
        var letter = FindAvailableDriveLetter();
        var args = $"-a -f \"{path}\" -m {letter}:";

        // For floppy images, pass the floppy device type
        if (media is DriveMediaType.Floppy35DD or DriveMediaType.Floppy35HD
                 or DriveMediaType.Floppy525DD or DriveMediaType.Floppy525HD)
            args += " -o fd";

        await RunProcessAsync("imdisk", args, ct);
        return letter;
    }

    private char MountViaVirtDiskApi(string vhdPath)
    {
        var storageType = new VIRTUAL_STORAGE_TYPE
        {
            DeviceId = 2, // VIRTUAL_STORAGE_TYPE_DEVICE_VHD
            VendorId = VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
        };

        int result = OpenVirtualDisk(
            ref storageType, vhdPath,
            0x00000003, // VIRTUAL_DISK_ACCESS_ATTACH_RO | VIRTUAL_DISK_ACCESS_GET_INFO
            0x00000001, // OPEN_VIRTUAL_DISK_FLAG_NONE + read-only
            IntPtr.Zero, out IntPtr handle);

        if (result != 0)
            throw new InvalidOperationException($"OpenVirtualDisk failed: 0x{result:X8}");

        result = AttachVirtualDisk(handle, IntPtr.Zero,
            0x00000001, // ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY
            0, IntPtr.Zero, IntPtr.Zero);

        if (result != 0)
        {
            CloseHandle(handle);
            throw new InvalidOperationException($"AttachVirtualDisk failed: 0x{result:X8}");
        }

        // The system assigns a drive letter automatically; find it.
        System.Threading.Thread.Sleep(1500);
        return FindNewlyMountedDrive();
    }

    private async Task<char> MountViaDiscUtils(string imagePath, DriveMediaType mediaType, CancellationToken ct)
    {
        var letter = FindAvailableDriveLetter();
        var ext = Path.GetExtension(imagePath).ToLowerInvariant();

        if (mediaType is DriveMediaType.Floppy35DD or DriveMediaType.Floppy35HD
            or DriveMediaType.Floppy525DD or DriveMediaType.Floppy525HD)
        {
            return await MountFloppyImageAsync(imagePath, letter, ct);
        }            

        throw new NotSupportedException($"DiscUtils backend does not support media type {mediaType}.");
    }

    private async Task<char> MountFloppyImageAsync(string imagePath, char letter, CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"RPMulate_{letter}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        await Task.Run(() =>
        {
            using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read);

            var fileSys = DetectFileSystem(fs);

            if (fs != null)
            {
                ExtractFilesRecursive(fileSys.Root, tempDir);
            }
            else
            {
                throw new NotSupportedException("Unable to detect filesystem in floppy image.");
            }
        }, ct);

        var substCmd = $"subst {letter}: \"{tempDir}\"";
        var psi = new ProcessStartInfo("cmd.exe", $"/c {substCmd}")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            Directory.Delete(tempDir, true);
            throw new InvalidOperationException($"Failed to mount floppy image: subst exited with {proc.ExitCode}");
        }

        _mountedTempDirs[letter] = tempDir;

        return letter;
    }

    private DiscFileSystem? DetectFileSystem(Stream stream)
    {
        stream.Position = 0;
        if (FatFileSystem.Detect(stream))
        {
            stream.Position = 0;
            return new FatFileSystem(stream);
        }

        return null;
    }

    private void ExtractFilesRecursive(DiscDirectoryInfo sourceDir, string targetDir)
    {
        foreach (var file in sourceDir.GetFiles())
        {
            var targetPath = Path.Combine(targetDir, file.Name);
            using var sourceStream = file.OpenRead();
            using var targetStream = File.Create(targetPath);
            sourceStream.CopyTo(targetStream);

            File.SetCreationTime(targetPath, file.CreationTime);
            File.SetLastWriteTime(targetPath, file.LastWriteTime);
        }

        foreach (var dir in sourceDir.GetDirectories())
        {
            var targetPath = Path.Combine(targetDir, dir.Name);
            Directory.CreateDirectory(targetPath);
            ExtractFilesRecursive(dir, targetPath);
        }
    }

    private readonly Dictionary<char, string> _mountedTempDirs = new();

    // -- Utilities ---------------------------------------------------

    private static bool IsWindows8OrLater() =>
        Environment.OSVersion.Version >= new Version(6, 2);

    private static bool IsImDiskInstalled()
    {
        try
        {
            var startInfo = new ProcessStartInfo("imdisk", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(startInfo);
            proc?.WaitForExit(2000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<string> RunPowerShellAsync(string script, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -Command \"{script}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start PowerShell.");
        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return output;
    }

    private static async Task<string> RunProcessAsync(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Cannot start {exe}.");
        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"{exe} failed (exit {proc.ExitCode}): {err}");
        }
        return output;
    }

    private static char FindNewlyMountedDrive()
    {
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType == DriveType.CDRom && d.IsReady)
                return d.Name[0];
        }
        return FindAvailableDriveLetter();
    }

    public void Dispose() { /* release any lingering handles */ }
}

/// <summary>
/// Throttles I/O reads and writes to simulate realistic drive speeds.
/// Wraps an underlying Stream and delays reads/writes to match the
/// selected SpeedProfile.
/// </summary>
public class ThrottledStream : Stream
{
    private readonly Stream _inner;
    private readonly SpeedProfile _profile;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _totalBytesRead;
    private long _totalBytesWritten;

    public event Action<long, double>? ReadProgress;  // (totalBytes, currentBytesPerSec)
    public event Action<long, double>? WriteProgress;

    public ThrottledStream(Stream inner, SpeedProfile profile)
    {
        _inner = inner;
        _profile = profile;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = _inner.Read(buffer, offset, count);
        if (!_profile.IsMaxSpeed && _profile.ReadBytesPerSecond > 0)
        {
            _totalBytesRead += bytesRead;
            ThrottleIfNeeded(_totalBytesRead, _profile.ReadBytesPerSecond);
            ReadProgress?.Invoke(_totalBytesRead,
                _totalBytesRead / _sw.Elapsed.TotalSeconds);
        }
        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        if (!_profile.IsMaxSpeed && _profile.WriteBytesPerSecond > 0)
        {
            _totalBytesWritten += count;
            ThrottleIfNeeded(_totalBytesWritten, _profile.WriteBytesPerSecond);
            WriteProgress?.Invoke(_totalBytesWritten,
                _totalBytesWritten / _sw.Elapsed.TotalSeconds);
        }
    }

    private void ThrottleIfNeeded(long totalBytes, long targetBytesPerSec)
    {
        double expectedSeconds = (double)totalBytes / targetBytesPerSec;
        double elapsed = _sw.Elapsed.TotalSeconds;
        if (elapsed < expectedSeconds)
        {
            int delayMs = (int)((expectedSeconds - elapsed) * 1000);
            if (delayMs > 0) Thread.Sleep(delayMs);
        }
    }

    // -- Passthrough plumbing --
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
}
