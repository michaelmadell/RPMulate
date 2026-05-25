using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RPMulate.Models;
using RPMulate.ViewModels;

namespace RPMulate.Views
{
    public partial class MainWindow : Window
    {
        private static readonly string[] SupportedExtensions =
            ImageFormats.Supported.Select(f => f.Extension).ToArray();

        public MainWindow()
        {
            InitializeComponent();
        }

        // -- Window-level drag & drop (auto-select first empty slot) --
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                e.Effects = files.Any(IsImageFile)
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            var images = files.Where(IsImageFile).ToArray();

            if (images.Length == 0) return;

            var vm = (MainViewModel)DataContext;

            foreach (var path in images)
            {
                // Find the first empty slot
                var slot = vm.DriveSlots.FirstOrDefault(s => s.IsEmpty && !s.HasImage);
                if (slot is null)
                {
                    // Auto-add a slot if room
                    if (vm.DriveSlots.Count < 8)
                    {
                        vm.AddSlotCommand.Execute(null);
                        slot = vm.DriveSlots.Last();
                    }
                    else break;
                }
                slot.SetImage(path);
            }
        }

        // -- Card-level drag & drop (target specific slot) ------------
        private void DriveCard_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                var slot = GetSlotFromSender(sender);
                e.Effects = files.Any(IsImageFile) && slot?.IsEmpty == true
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DriveCard_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var slot = GetSlotFromSender(sender);
            if (slot is null || !slot.IsEmpty) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            var image = files.FirstOrDefault(IsImageFile);
            if (image is not null)
                slot.SetImage(image);

            e.Handled = true;
        }

        // -- Helpers --------------------------------------------------
        private static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return SupportedExtensions.Contains(ext);
        }

        private static DriveSlotViewModel? GetSlotFromSender(object sender)
        {
            return sender is FrameworkElement fe ? fe.DataContext as DriveSlotViewModel : null;
        }
    }
}
