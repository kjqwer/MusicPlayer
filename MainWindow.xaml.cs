using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using MusicPlayer.Models;
using MusicPlayer.Services;
using MusicPlayer.ViewModels;

// 解决WPF和WinForms的命名空间冲突
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;
using MenuItem = System.Windows.Controls.MenuItem;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;                                                                                                                                                                       
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MusicPlayer; 

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly MediaSessionService _mediaSessionService;
    private bool _isCompactMode;
    private double _normalWidth, _normalHeight, _normalLeft, _normalTop;
    private bool _isDraggingSlider;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel();
        _hotkeyService = new GlobalHotkeyService();
        _mediaSessionService = new MediaSessionService();
        
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 注册全局媒体按键 (WM_APPCOMMAND)
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Register(handle);
        _hotkeyService.PlayPausePressed += (s, e) => Dispatcher.Invoke(() => _viewModel.TogglePlayPause());
        _hotkeyService.NextPressed += (s, e) => Dispatcher.Invoke(() => _viewModel.PlayNext());
        _hotkeyService.PreviousPressed += (s, e) => Dispatcher.Invoke(() => _viewModel.PlayPrevious());
        _hotkeyService.StopPressed += (s, e) => Dispatcher.Invoke(() => _viewModel.Stop());
        
        // 注册系统媒体会话（Windows Media Session API - 与Chrome等应用相同）
        _mediaSessionService.PlayPausePressed += (s, e) => _viewModel.TogglePlayPause();
        _mediaSessionService.NextPressed += (s, e) => _viewModel.PlayNext();
        _mediaSessionService.PreviousPressed += (s, e) => _viewModel.PlayPrevious();
        _mediaSessionService.StopPressed += (s, e) => _viewModel.Stop();
        _mediaSessionService.Start();
        
        // 绑定ViewModel事件以更新系统媒体信息
        _viewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsPlaying))
            {
                _mediaSessionService.UpdatePlaybackStatus(_viewModel.IsPlaying);
            }
            else if (args.PropertyName == nameof(MainViewModel.CurrentSong))
            {
                _mediaSessionService.UpdateMetadata(_viewModel.CurrentSong);
            }
        };

        // 初始化ViewModel
        await _viewModel.InitializeAsync();
        
        // 恢复窗口位置
        if (_viewModel.Settings.WindowWidth > 0)
        {
            Width = _viewModel.Settings.WindowWidth;
            Height = _viewModel.Settings.WindowHeight;
            Left = _viewModel.Settings.WindowLeft;
            Top = _viewModel.Settings.WindowTop;
        }
        
        if (_viewModel.Settings.CompactMode)
            EnterCompactMode();
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // 保存设置
        _viewModel.Settings.WindowWidth = Width;
        _viewModel.Settings.WindowHeight = Height;
        _viewModel.Settings.WindowLeft = Left;
        _viewModel.Settings.WindowTop = Top;
        _viewModel.Settings.CompactMode = _isCompactMode;
        
        await _viewModel.SaveAsync();
        
        _hotkeyService.Dispose();
        _mediaSessionService.Dispose();
        _viewModel.Dispose();
        TrayIcon.Dispose();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _viewModel.Settings.MinimizeToTray)
        {
            Hide();
            TrayIcon.ShowBalloonTip("HiFi Player", "已最小化到系统托盘", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }

    #region 标题栏操作

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleCompactMode();
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Compact_Click(object sender, RoutedEventArgs e)
    {
        ToggleCompactMode();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Settings.MinimizeToTray)
        {
            Hide();
            TrayIcon.ShowBalloonTip("HiFi Player", "已最小化到系统托盘，右键图标可退出", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
        else
        {
            Close();
        }
    }

    private void ToggleCompactMode()
    {
        if (_isCompactMode)
            ExitCompactMode();
        else
            EnterCompactMode();
    }

    private void EnterCompactMode()
    {
        _normalWidth = Width;
        _normalHeight = Height;
        _normalLeft = Left;
        _normalTop = Top;
        
        Width = 320;
        Height = 200;
        
        // 移动到屏幕右下角
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
        
        _isCompactMode = true;
        _viewModel.CompactMode = true;
    }

    private void ExitCompactMode()
    {
        Width = _normalWidth > 0 ? _normalWidth : 420;
        Height = _normalHeight > 0 ? _normalHeight : 640;
        Left = _normalLeft;
        Top = _normalTop;
        
        _isCompactMode = false;
        _viewModel.CompactMode = false;
    }

    #endregion

    #region 系统托盘

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    #endregion

    #region 播放控制

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.TogglePlayPause();
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PlayPrevious();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PlayNext();
    }

    private void Shuffle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShuffleMode = !_viewModel.ShuffleMode;
    }

    private void Repeat_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CycleRepeatMode();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearSearch();
        SearchBox.Focus();
    }

    private void ProgressSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void ProgressSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isDraggingSlider && sender is Slider slider)
        {
            _viewModel.Seek(slider.Value);
        }
        _isDraggingSlider = false;
    }

    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider slider)
        {
            // 计算点击位置对应的时间
            var point = e.GetPosition(slider);
            var percent = point.X / slider.ActualWidth;
            var time = slider.Maximum * percent;
            
            slider.Value = time;
            _viewModel.Seek(time);
        }
    }

    private void SongList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SongList.SelectedItem is MusicFile song)
        {
            _viewModel.PlaySong(song);
        }
    }

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MusicFile song)
        {
            _viewModel.ToggleFavorite(song);
        }
    }

    private void CurrentSongFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentSong != null)
        {
            _viewModel.ToggleFavorite(_viewModel.CurrentSong);
        }
    }

    #endregion

    #region 播放列表操作

    private void NewPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.InputDialog("新建播放列表", "请输入播放列表名称:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var playlist = _viewModel.CreateCustomPlaylist(dialog.InputText);
            _viewModel.CurrentPlaylist = playlist;
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择音乐文件夹",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var name = Path.GetFileName(dialog.SelectedPath) ?? "新文件夹";
            
            // 显示加载进度
            var loadingDialog = new Views.LoadingDialog("扫描音乐文件...");
            loadingDialog.Owner = this;
            
            var progress = new Progress<int>(percent =>
            {
                loadingDialog.UpdateProgress(percent);
            });
            
            // 异步加载
            var task = Task.Run(async () =>
            {
                var playlist = await _viewModel.CreateFolderPlaylistAsync(name, new[] { dialog.SelectedPath }, progress);
                return playlist;
            });
            
            loadingDialog.Show();
            var result = await task;
            loadingDialog.Close();
            
            _viewModel.CurrentPlaylist = result;
        }
    }

    private async void ImportPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "播放列表文件 (*.json)|*.json",
            Title = "导入播放列表"
        };

        if (dialog.ShowDialog() == true)
        {
            var playlist = await _viewModel.ImportPlaylistAsync(dialog.FileName);
            if (playlist != null)
            {
                _viewModel.CurrentPlaylist = playlist;
                MessageBox.Show($"已导入播放列表: {playlist.Name}", "导入成功");
            }
            else
            {
                MessageBox.Show("无法导入播放列表", "错误");
            }
        }
    }

    private async void ExportPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentPlaylist == null)
        {
            MessageBox.Show("请先选择一个播放列表", "提示");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "播放列表文件 (*.json)|*.json",
            Title = "导出播放列表",
            FileName = $"{_viewModel.CurrentPlaylist.Name}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.ExportPlaylistAsync(_viewModel.CurrentPlaylist, dialog.FileName);
            MessageBox.Show("播放列表已导出", "导出成功");
        }
    }

    private async void AddToPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is Playlist playlist)
        {
            MusicFile? targetSong = null;

            if (menuItem.CommandParameter is MusicFile paramSong)
            {
                targetSong = paramSong;
            }
            else if (SongList.SelectedItem is MusicFile selectedSong)
            {
                targetSong = selectedSong;
            }

            if (targetSong != null)
            {
                await _viewModel.AddFilesToPlaylistAsync(playlist, new[] { targetSong.FilePath });
            }
        }
    }

    private void SongContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        // 动态生成播放列表子菜单
        AddToPlaylistMenu.Items.Clear();
        
        foreach (var playlist in _viewModel.Playlists.Where(p => p.Type == PlaylistType.Custom || p.Type == PlaylistType.Folder))
        {
            var menuItem = new MenuItem { Header = playlist.Name, Tag = playlist };
            menuItem.Click += async (s, args) =>
            {
                if (SongList.SelectedItem is MusicFile song)
                {
                    await _viewModel.AddFilesToPlaylistAsync(playlist, new[] { song.FilePath });
                    MessageBox.Show($"已添加到 {playlist.Name}", "成功");
                }
            };
            AddToPlaylistMenu.Items.Add(menuItem);
        }
        
        if (AddToPlaylistMenu.Items.Count == 0)
        {
            AddToPlaylistMenu.Items.Add(new MenuItem { Header = "(无可用播放列表)", IsEnabled = false });
        }
    }

    private void ContextMenu_ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (SongList.SelectedItem is MusicFile song)
        {
            _viewModel.ToggleFavorite(song);
        }
    }

    private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistList.SelectedItem is Playlist playlist)
        {
            if (playlist.Type == PlaylistType.Favorites)
            {
                MessageBox.Show("收藏夹不能删除", "提示");
                return;
            }
            
            var result = MessageBox.Show($"确定要删除播放列表 \"{playlist.Name}\" 吗？", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.DeletePlaylist(playlist);
            }
        }
    }

    private async void RefreshPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistList.SelectedItem is Playlist playlist && playlist.Type == PlaylistType.Folder)
        {
            await _viewModel.RefreshFolderPlaylistAsync(playlist);
            MessageBox.Show("播放列表已刷新", "成功");
        }
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private async void SongList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var audioFiles = files.Where(f => 
                new[] { ".mp3", ".flac", ".wav", ".aac", ".m4a", ".ogg", ".wma", ".aiff", ".ape" }
                    .Contains(Path.GetExtension(f).ToLower())).ToArray();

            if (audioFiles.Length > 0)
            {
                // 如果没有当前播放列表，创建一个
                if (_viewModel.CurrentPlaylist == null || _viewModel.CurrentPlaylist.Type == PlaylistType.Favorites)
                {
                    var playlist = _viewModel.CreateCustomPlaylist("我的音乐");
                    _viewModel.CurrentPlaylist = playlist;
                }

                await _viewModel.AddFilesToPlaylistAsync(_viewModel.CurrentPlaylist, audioFiles);
            }
        }
    }

    #endregion
}
