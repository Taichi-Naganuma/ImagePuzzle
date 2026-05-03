using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ImagePuzzle
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _itemService = new ItemService();
            _settings = AppSettings.Load();

            _property = new Property();
            _property.Visibility = Visibility.Collapsed;
            _methods = new Methods(_property);
            MethodItems = new ObservableCollection<MethodItem>(_methods.AddMethod());

            LocalizationService.LanguageChanged += (s, e) =>
            {
                foreach (var item in MethodItems) item.RefreshDisplayName();
                foreach (var item in ExeItems) item.RefreshDisplayName();
            };

            if (_settings.OutputFolderPath != null && Directory.Exists(_settings.OutputFolderPath))
            {
                FolderPath = _settings.OutputFolderPath;
                OutputLabel.Content = Path.GetFileName(FolderPath);
            }
        }

        private readonly Methods _methods;
        private readonly ItemService _itemService;
        private AppSettings _settings;
        private readonly Property _property;

        ListBox? MethodDrag;
        ListBox? ExeDrag;
        ExeItem? placeholder;
        ExeItem? DragItem;
        FileItem? fileplace;
        FileItem? file;
        FileItem? Remofile;
        ExeItem? RemoItem;
        Point? _exeDragStart;
        Point? _fileDragStart;

        public string? FolderPath;

        public ObservableCollection<MethodItem> MethodItems { get; }
        public ObservableCollection<ExeItem> ExeItems { get; } = new();
        public ObservableCollection<FileItem> FileItems { get; } = new();

        public class MethodItem : BaseItemsDefine
        {
            public virtual MethodItem Clone() => new MethodItem
            {
                DisplayName = DisplayName,
                DisplayNameKey = DisplayNameKey,
                ExecuteAsync = ExecuteAsync,
            };
        }

        public class FileItem : BaseItemsDefine
        {
            public string? Path { get; set; }
            public string? DisplayPath { get; set; }
        }

        public class ExeItem : BaseItemsDefine { }

        public class BaseItemsDefine : INotifyPropertyChanged
        {
            private string? _displayName;
            public string? DisplayName
            {
                get => _displayName;
                set { _displayName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName))); }
            }
            public string? DisplayNameKey { get; set; }
            public void RefreshDisplayName()
            {
                if (DisplayNameKey != null) DisplayName = LocalizationService.Get(DisplayNameKey);
            }
            public Func<IProgress<string>, Task>? ExecuteAsync { get; set; }
            public bool IsChecked { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        // ===== Method List =====
        private void MethodList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
        private void MethodList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => CleanDragDropData();
        private void MethodList_DragEnter(object sender, DragEventArgs e) { }
        private void MethodList_MouseLeave(object sender, MouseEventArgs e) { }
        private void MethodList_DragOver(object sender, DragEventArgs e)
        {
            _itemService.WhileDropCopy(e, typeof(MethodItem));
            _itemService.WhileDropNone(e, typeof(ExeItem));
        }
        private void MethodList_Drop(object sender, DragEventArgs e) => CleanDragDropData();
        private void MethodList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (ExeDrag == ExeList) return;
            MethodDrag = MethodList;
            MethodItem? selected = (MethodItem)MethodList.SelectedItem;
            if (selected != null)
            {
                DragItem = _itemService.ConvertToExeitem(selected.Clone());
                DragDrop.DoDragDrop(MethodList, DragItem, DragDropEffects.Copy);
            }
        }

        // ===== Exe List =====
        private void ExeList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ExeDrag = ExeList;
            DragItem = ExeList.SelectedItem as ExeItem;
            if (DragItem != null) _exeDragStart = e.GetPosition(ExeList);
        }
        private void ExeList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _exeDragStart = null;
            CleanDragDropData();
        }
        private void ExeList_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ExeItem)) && !e.Data.GetDataPresent(typeof(MethodItem))) return;
            if (placeholder == null && DragItem != null)
            {
                placeholder = _itemService.CreatePlaceholder();
                ExeItems.Add(placeholder);
            }
        }
        private void ExeList_DragOver(object sender, DragEventArgs e)
        {
            _itemService.WhileDropCopy(e, typeof(MethodItem));
            _itemService.WhileDropCopy(e, typeof(ExeItem));
            if (placeholder == null) return;
            Point pos = e.GetPosition(ExeList);
            int targetIndex = _itemService.GetUnderIndex(ExeList, pos, ExeItems);
            _itemService.MovePlaceholder(placeholder, ExeList, pos, targetIndex, ExeItems, e);
        }
        private void ExeList_Drop(object sender, DragEventArgs e)
        {
            if (DragItem == null) { CleanDragDropData(); return; }
            if (placeholder != null)
            {
                int idx = ExeItems.IndexOf(placeholder);
                ExeItems.Remove(placeholder);
                placeholder = null;
                ExeItems.Remove(DragItem);
                if (idx > ExeItems.Count) idx = ExeItems.Count;
                ExeItems.Insert(idx, DragItem);
            }
            else
            {
                ExeItems.Remove(DragItem);
                ExeItems.Add(DragItem);
            }
            DragItem = null; ExeDrag = null; MethodDrag = null;
        }
        private void ExeList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || DragItem == null || ExeDrag != ExeList || _exeDragStart == null) return;
            Point pos = e.GetPosition(ExeList);
            if (Math.Abs(pos.X - _exeDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _exeDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _exeDragStart = null;
            if (placeholder == null)
            {
                placeholder = _itemService.CreatePlaceholder();
                int idx = ExeItems.IndexOf(DragItem);
                ExeItems.Insert(idx, placeholder);
                ExeItems.Remove(DragItem);
            }
            DragDrop.DoDragDrop(ExeList, DragItem, DragDropEffects.Move);
            CleanDragDropData();
        }

        // ===== Trash Drop =====
        private void Rectangle_DragOver(object sender, DragEventArgs e)
        {
            _itemService.WhileDropCopy(e, typeof(ExeItem));
            _itemService.WhileDropCopy(e, typeof(MethodItem));
        }
        private void Rectangle_Drop(object sender, DragEventArgs e) => CleanDragDropData();

        // ===== File List =====
        private void FileList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            file = FileList.SelectedItem as FileItem;
            if (file != null) _fileDragStart = e.GetPosition(FileList);
        }
        private void FileList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _fileDragStart = null;
            CleanDragDropData();
        }
        private void FileList_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }
        private void FileList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || file == null || _fileDragStart == null) return;
            Point pos = e.GetPosition(FileList);
            if (Math.Abs(pos.X - _fileDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _fileDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _fileDragStart = null;
            if (fileplace == null)
            {
                fileplace = _itemService.Createfileplace();
                int idx = FileItems.IndexOf(file);
                FileItems.Insert(idx, fileplace);
                FileItems.Remove(file);
            }
            DragDrop.DoDragDrop(FileList, file, DragDropEffects.Move);
            CleanDragDropData();
        }
        private void FileList_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(typeof(FileItem)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            if (fileplace == null) return;
            Point pos = e.GetPosition(FileList);
            int underIndex = _itemService.GetFileIndex(FileList, pos, FileItems);
            _itemService.ShowInsertLine(pos, underIndex, FileList, fileplace, FileItems, e);
        }
        private void FileList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var f in dropped.Where(f => _itemService.IsImageFile(f)))
                    FileItems.Add(new FileItem { Path = f, DisplayPath = System.IO.Path.GetFileName(f) });
                CleanDragDropData();
                return;
            }
            if (e.Data.GetDataPresent(typeof(FileItem)) && file != null)
            {
                int idx = fileplace != null ? FileItems.IndexOf(fileplace) : FileItems.Count;
                if (fileplace != null) { FileItems.Remove(fileplace); fileplace = null; }
                FileItems.Remove(file);
                if (idx > FileItems.Count) idx = FileItems.Count;
                FileItems.Insert(idx, file);
                file = null;
            }
            CleanDragDropData();
        }

        // ===== Buttons =====
        private void Add_Exe_Button_Click(object sender, RoutedEventArgs e)
        {
            if (MethodList.SelectedItem == null && MethodItems.Count > 0)
                MethodList.SelectedIndex = 0;

            var selected = MethodList.SelectedItem as MethodItem;
            if (selected == null)
            {
                StatusLabel.Content = L("Msg_SelectMethod");
                StatusLabel.Foreground = System.Windows.Media.Brushes.OrangeRed;
                return;
            }

            ExeItems.Add(_itemService.ConvertToExeitem(selected.Clone())!);
            StatusLabel.Content = string.Empty;
        }

        private void Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            Remofile = (FileItem)FileList.SelectedItem;
            RemoItem = (ExeItem)ExeList.SelectedItem;
            if (RemoItem != null) ExeItems.Remove(RemoItem);
            if (Remofile != null) FileItems.Remove(Remofile);
        }

        private void Ref_Button_Click(object sender, RoutedEventArgs e)
        {
            var files = _itemService.SelectFile();
            if (files == null) return;
            foreach (var f in files)
                FileItems.Add(new FileItem { Path = f, DisplayPath = System.IO.Path.GetFileName(f) });
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            string? folder = _itemService.SelectFolder();
            if (folder == null) return;
            FolderPath = folder;
            OutputLabel.Content = System.IO.Path.GetFileName(folder);
            _settings = AppSettings.Load();
            _settings.OutputFolderPath = folder;
            _settings.Save();
        }

        private async void Exe_Button_Click(object sender, RoutedEventArgs e)
        {
            if (ExeItems.Count == 0 || FileItems.Count == 0)
            {
                StatusLabel.Content = L("Msg_NeedFileAndAction");
                StatusLabel.Foreground = System.Windows.Media.Brushes.OrangeRed;
                return;
            }
            if (string.IsNullOrEmpty(FolderPath))
            {
                string? folder = _itemService.SelectFolder();
                if (folder == null) return;
                FolderPath = folder;
                OutputLabel.Content = System.IO.Path.GetFileName(folder);
            }

            Exe_Button.IsEnabled = false;
            ExecuteProgressBar.Visibility = Visibility.Visible;
            StatusLabel.Foreground = System.Windows.Media.Brushes.DarkGreen;

            var progress = new Progress<string>(fileName =>
                Dispatcher.Invoke(() => StatusLabel.Content = $"{L("Status_Processing")} {fileName}"));

            try
            {
                foreach (var exeItem in ExeItems.ToList())
                {
                    if (exeItem.ExecuteAsync != null)
                        await exeItem.ExecuteAsync(progress);
                }
                StatusLabel.Content = L("Status_Done");
                var s = AppSettings.Load();
                if (s.OpenFolderAfterExecution && FolderPath != null)
                    System.Diagnostics.Process.Start("explorer.exe", FolderPath);
            }
            catch (Exception ex)
            {
                StatusLabel.Content = ex.Message;
                StatusLabel.Foreground = System.Windows.Media.Brushes.Crimson;
            }
            finally
            {
                Exe_Button.IsEnabled = true;
                ExecuteProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            _property.Owner = this;
            _property.Show();
        }

        private void Exit_Button_Click(object sender, RoutedEventArgs e) => Close();

        // ===== Drag/Drop Cleanup =====
        public void CleanDragDropData()
        {
            bool wasExeReorder = ExeDrag == ExeList;
            _exeDragStart = null;
            _fileDragStart = null;
            ExeDrag = null; MethodDrag = null;

            if (placeholder != null)
            {
                int idx = ExeItems.IndexOf(placeholder);
                ExeItems.Remove(placeholder);
                placeholder = null;
                if (wasExeReorder && DragItem != null && !ExeItems.Contains(DragItem))
                    ExeItems.Insert(Math.Clamp(idx, 0, ExeItems.Count), DragItem);
            }
            DragItem = null;

            if (fileplace != null)
            {
                int idx = FileItems.IndexOf(fileplace);
                FileItems.Remove(fileplace);
                fileplace = null;
                if (file != null && !FileItems.Contains(file))
                    FileItems.Insert(Math.Clamp(idx, 0, FileItems.Count), file);
            }
            file = null;
        }

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e) { }
        private void Window_Drop(object sender, DragEventArgs e) => CleanDragDropData();
        private void Window_QueryContinueDrag(object sender, QueryContinueDragEventArgs e) { }

        private static string L(string key) => LocalizationService.Get(key);
    }
}
