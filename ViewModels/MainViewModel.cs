using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using MusicPlayer.Models;
using MusicPlayer.Services;

namespace MusicPlayer.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AudioEngine _audioEngine;
    private readonly PlaylistService _playlistService;
    private readonly DispatcherTimer _positionTimer;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly Random _random = new();
    private readonly List<int> _shuffleHistory = []; // 随机播放历史
    private Playlist? _playbackPlaylist;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _audioEngine = new AudioEngine();
        _playlistService = new PlaylistService();
        
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _positionTimer.Tick += (s, e) => UpdatePosition();
        
        // 自动保存定时器：每30秒保存一次状态（防止强制关闭丢失）
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autoSaveTimer.Tick += async (s, e) => await SaveAsync();
        _autoSaveTimer.Start();
        
        _audioEngine.PlaybackStopped += OnPlaybackStopped;
        _audioEngine.PlaybackFailed += OnPlaybackFailed;
        
        Songs = [];
        Playlists = [];
    }

    #region Properties

    public ObservableCollection<MusicFile> Songs { get; }
    public ObservableCollection<Playlist> Playlists { get; }

    private MusicFile? _currentSong;
    public MusicFile? CurrentSong
    {
        get => _currentSong;
        set { _currentSong = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCurrentSong)); }
    }

    public bool HasCurrentSong => CurrentSong != null;

    private MusicFile? _selectedSong;
    public MusicFile? SelectedSong
    {
        get => _selectedSong;
        set { _selectedSong = value; OnPropertyChanged(); }
    }

    private Playlist? _currentPlaylist;
    public Playlist? CurrentPlaylist
    {
        get => _currentPlaylist;
        set { _currentPlaylist = value; OnPropertyChanged(); LoadPlaylistSongs(); }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    private double _currentPosition;
    public double CurrentPosition
    {
        get => _currentPosition;
        set
        {
            _currentPosition = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentPositionText));
        }
    }

    private double _totalDuration = 1;
    public double TotalDuration
    {
        get => _totalDuration;
        set
        {
            _totalDuration = value > 0 ? value : 1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalDurationText));
        }
    }

    public string CurrentPositionText => TimeSpan.FromSeconds(CurrentPosition).ToString(@"mm\:ss");
    public string TotalDurationText => TimeSpan.FromSeconds(TotalDuration).ToString(@"mm\:ss");

    private double _volume = 0.7;
    public double Volume
    {
        get => _volume;
        set 
        { 
            _volume = Math.Clamp(value, 0, 1); 
            _audioEngine.Volume = (float)_volume;
            OnPropertyChanged(); 
        }
    }

    private bool _shuffleMode;
    public bool ShuffleMode
    {
        get => _shuffleMode;
        set { _shuffleMode = value; OnPropertyChanged(); }
    }

    private RepeatMode _repeatMode;
    public RepeatMode RepeatMode
    {
        get => _repeatMode;
        set { _repeatMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(RepeatModeText)); }
    }

    public string RepeatModeText => RepeatMode switch
    {
        RepeatMode.None => "↻",
        RepeatMode.All => "↻",
        RepeatMode.One => "↻1",
        _ => "↻"
    };

    private bool _compactMode;
    public bool CompactMode
    {
        get => _compactMode;
        set { _compactMode = value; OnPropertyChanged(); }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set 
        { 
            _searchText = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(HasSearchText));
            FilterSongs();
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    private int _filteredSongsCount;
    public int FilteredSongsCount
    {
        get => _filteredSongsCount;
        set { _filteredSongsCount = value; OnPropertyChanged(); }
    }

    private readonly List<MusicFile> _allSongs = []; // 保存原始列表用于搜索

    public Playlist Favorites => _playlistService.Favorites;
    public AppSettings Settings => _playlistService.Settings;

    #endregion

    #region Methods

    public async Task InitializeAsync()
    {
        await _playlistService.LoadAsync();
        
        // 恢复设置
        Volume = _playlistService.Settings.Volume;
        ShuffleMode = _playlistService.Settings.ShuffleMode;
        RepeatMode = _playlistService.Settings.RepeatMode;
        CompactMode = _playlistService.Settings.CompactMode;
        _audioEngine.Latency = _playlistService.Settings.AudioLatency;
        
        RefreshPlaylists();
        
        // 恢复上次播放状态
        await RestoreLastPlaybackAsync();
    }

    /// <summary>
    /// 恢复上次的播放状态（播放列表、歌曲、位置）
    /// </summary>
    private Task RestoreLastPlaybackAsync()
    {
        var settings = _playlistService.Settings;
        
        // 恢复播放列表
        if (!string.IsNullOrEmpty(settings.LastPlaylistId))
        {
            var playlist = Playlists.FirstOrDefault(p => p.Id == settings.LastPlaylistId);
            if (playlist != null)
            {
                SetPlaybackPlaylist(playlist);
                CurrentPlaylist ??= playlist;
                
                // 恢复歌曲
                if (!string.IsNullOrEmpty(settings.LastPlayedSongPath))
                {
                    var song = playlist.Songs.FirstOrDefault(s => 
                        s.FilePath.Equals(settings.LastPlayedSongPath, StringComparison.OrdinalIgnoreCase));
                    
                    if (song != null)
                    {
                        // 加载歌曲但不自动播放，只是准备好
                        try
                        {
                            _audioEngine.Load(song.FilePath);
                            CurrentSong = song;
                            TotalDuration = _audioEngine.TotalDuration.TotalSeconds;
                            
                            // 恢复播放位置
                            if (settings.LastPosition > 0 && settings.LastPosition < TotalDuration)
                            {
                                _audioEngine.Seek(TimeSpan.FromSeconds(settings.LastPosition));
                                CurrentPosition = settings.LastPosition;
                            }
                            
                            // 不自动播放，等待用户点击
                            IsPlaying = false;
                        }
                        catch { }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task SaveAsync()
    {
        _playlistService.Settings.Volume = Volume;
        _playlistService.Settings.ShuffleMode = ShuffleMode;
        _playlistService.Settings.RepeatMode = RepeatMode;
        _playlistService.Settings.CompactMode = CompactMode;
        _playlistService.Settings.LastPlayedSongPath = CurrentSong?.FilePath;
        _playlistService.Settings.LastPlaylistId = _playbackPlaylist?.Id;
        _playlistService.Settings.LastPosition = CurrentPosition;
        
        await _playlistService.SaveAsync();
    }

    public void RefreshPlaylists()
    {
        Playlists.Clear();
        Playlists.Add(_playlistService.Favorites);
        foreach (var pl in _playlistService.Playlists)
            Playlists.Add(pl);
    }

    private void LoadPlaylistSongs()
    {
        Songs.Clear();
        _allSongs.Clear();
        SearchText = string.Empty; // 切换播放列表时清空搜索
        
        if (CurrentPlaylist == null) return;
        
        foreach (var song in CurrentPlaylist.Songs)
        {
            Songs.Add(song);
            _allSongs.Add(song);
        }
        
        FilteredSongsCount = Songs.Count;
    }

    private void FilterSongs()
    {
        Songs.Clear();
        
        var filtered = string.IsNullOrWhiteSpace(SearchText) 
            ? _allSongs 
            : [.. _allSongs.Where(s => 
                s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Album.Contains(SearchText, StringComparison.OrdinalIgnoreCase))];
        
        foreach (var song in filtered)
            Songs.Add(song);
        
        FilteredSongsCount = Songs.Count;
    }

    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    public void PlaySong(MusicFile song)
    {
        try
        {
            if (_playbackPlaylist == null && CurrentPlaylist != null)
                SetPlaybackPlaylist(CurrentPlaylist);

            // 先停止定时器，避免在加载过程中更新位置
            _positionTimer.Stop();
            
            _audioEngine.Load(song.FilePath);
            
            CurrentSong = song;
            if (CurrentPlaylist != null && _playbackPlaylist != null && CurrentPlaylist.Id == _playbackPlaylist.Id)
                SelectedSong = song;
            TotalDuration = _audioEngine.TotalDuration.TotalSeconds;
            CurrentPosition = 0; // 确保在TotalDuration之后设置，避免百分比计算问题
            
            // 播放并启动定时器
            if (_audioEngine.Play())
            {
                IsPlaying = true;
                _positionTimer.Start();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法播放: {song.DisplayName}\n路径: {song.FilePath}\n原因: {ex.Message}", "错误");
        }
    }

    public void PlaySong(MusicFile song, Playlist? sourcePlaylist)
    {
        if (sourcePlaylist != null && (_playbackPlaylist == null || _playbackPlaylist.Id != sourcePlaylist.Id))
            SetPlaybackPlaylist(sourcePlaylist);
        PlaySong(song);
    }

    public void TogglePlayPause()
    {
        if (CurrentSong == null)
        {
            var playlist = _playbackPlaylist ?? CurrentPlaylist;
            if (playlist != null && playlist.Songs.Count > 0)
                PlaySong(playlist.Songs[0], playlist);
            return;
        }

        if (IsPlaying)
        {
            _audioEngine.Pause();
            IsPlaying = false;
            _positionTimer.Stop();
        }
        else
        {
            // 尝试播放，只有成功时才更新状态
            if (_audioEngine.Play())
            {
                IsPlaying = true;
                _positionTimer.Start();
            }
            // 如果播放失败，PlaybackFailed 事件会处理重新加载
        }
    }

    public void Stop()
    {
        _audioEngine.Stop();
        IsPlaying = false;
        CurrentPosition = 0;
        _positionTimer.Stop();
    }

    public void PlayNext()
    {
        var playbackSongs = _playbackPlaylist?.Songs;
        if (playbackSongs == null || playbackSongs.Count == 0) return;
        
        // 如果没有当前歌曲，播放第一首
        if (CurrentSong == null || !playbackSongs.Contains(CurrentSong))
        {
            PlaySong(playbackSongs[0]);
            return;
        }
        
        int currentIndex = playbackSongs.IndexOf(CurrentSong);
        int nextIndex;
        
        if (ShuffleMode)
        {
            // 智能随机：记录播放历史，避免连续重复
            _shuffleHistory.Add(currentIndex);
            
            // 如果所有歌曲都播放过，重置历史
            if (_shuffleHistory.Count >= playbackSongs.Count)
                _shuffleHistory.Clear();
            
            // 从未播放的歌曲中随机选择
            var unplayed = Enumerable.Range(0, playbackSongs.Count)
                .Except(_shuffleHistory)
                .ToList();
            
            if (unplayed.Count > 0)
                nextIndex = unplayed[_random.Next(unplayed.Count)];
            else
                nextIndex = _random.Next(playbackSongs.Count);
        }
        else
        {
            nextIndex = (currentIndex + 1) % playbackSongs.Count;
        }
        
        PlaySong(playbackSongs[nextIndex]);
    }

    public void PlayPrevious()
    {
        var playbackSongs = _playbackPlaylist?.Songs;
        if (playbackSongs == null || playbackSongs.Count == 0) return;
        
        if (CurrentSong == null || !playbackSongs.Contains(CurrentSong))
        {
            PlaySong(playbackSongs[0]);
            return;
        }
        
        // 如果播放超过3秒，回到开头
        if (CurrentPosition > 3)
        {
            Seek(0);
            return;
        }
        
        int currentIndex = playbackSongs.IndexOf(CurrentSong);
        
        if (ShuffleMode && _shuffleHistory.Count > 0)
        {
            // 随机模式：回到上一首播放的歌曲
            int prevIndex = _shuffleHistory[^1];
            _shuffleHistory.RemoveAt(_shuffleHistory.Count - 1);
            PlaySong(playbackSongs[prevIndex]);
        }
        else
        {
            int prevIndex = currentIndex > 0 ? currentIndex - 1 : playbackSongs.Count - 1;
            PlaySong(playbackSongs[prevIndex]);
        }
    }

    public void PlayPlaylist(Playlist playlist)
    {
        SetPlaybackPlaylist(playlist);
        if (playlist.Songs.Count > 0)
            PlaySong(playlist.Songs[0], playlist);
    }
    
    /// <summary>
    /// 切换播放列表时重置随机历史
    /// </summary>
    public void ResetShuffleHistory()
    {
        _shuffleHistory.Clear();
    }

    private void SetPlaybackPlaylist(Playlist playlist)
    {
        if (_playbackPlaylist != null && _playbackPlaylist.Id == playlist.Id) return;
        _playbackPlaylist = playlist;
        ResetShuffleHistory();
    }

    public void Seek(double seconds)
    {
        _audioEngine.Seek(TimeSpan.FromSeconds(seconds));
        CurrentPosition = seconds;
    }

    private void UpdatePosition()
    {
        CurrentPosition = _audioEngine.CurrentPosition.TotalSeconds;
    }

    /// <summary>
    /// 播放失败时的处理（如休眠后设备失效）
    /// 尝试重新加载并播放当前歌曲
    /// </summary>
    private void OnPlaybackFailed(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (CurrentSong == null) return;
            
            try
            {
                // 重新加载并播放当前歌曲
                var position = CurrentPosition; // 保存当前位置
                _audioEngine.Load(CurrentSong.FilePath);
                TotalDuration = _audioEngine.TotalDuration.TotalSeconds;
                
                // 如果之前有播放进度，恢复位置
                if (position > 0 && position < TotalDuration - 1)
                {
                    _audioEngine.Seek(TimeSpan.FromSeconds(position));
                }
                
                if (_audioEngine.Play())
                {
                    IsPlaying = true;
                    _positionTimer.Start();
                }
            }
            catch
            {
                // 如果重新加载也失败，提示用户
                IsPlaying = false;
                _positionTimer.Stop();
            }
        });
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var playbackSongs = _playbackPlaylist?.Songs;
            if (playbackSongs == null || playbackSongs.Count == 0) return;

            // 歌曲播放完成（不是手动停止）
            if (CurrentPosition >= TotalDuration - 0.5)
            {
                switch (RepeatMode)
                {
                    case RepeatMode.One:
                        // 单曲循环
                        Seek(0);
                        if (!_audioEngine.Play())
                        {
                            // 播放失败，PlaybackFailed 会处理重新加载
                            IsPlaying = false;
                            _positionTimer.Stop();
                        }
                        break;
                        
                    case RepeatMode.All:
                        // 列表循环（包括随机模式）
                        PlayNext();
                        break;
                        
                    default: // RepeatMode.None
                        int currentIndex = CurrentSong != null ? playbackSongs.IndexOf(CurrentSong) : -1;
                        
                        if (ShuffleMode)
                        {
                            // 随机模式下无循环：播放完所有未播放的歌曲后停止
                            if (_shuffleHistory.Count < playbackSongs.Count - 1)
                            {
                                PlayNext();
                            }
                            else
                            {
                                // 所有歌曲都播放过了
                                IsPlaying = false;
                                _positionTimer.Stop();
                                _shuffleHistory.Clear();
                            }
                        }
                        else
                        {
                            // 顺序模式：播放到最后一首后停止
                            if (currentIndex < playbackSongs.Count - 1)
                            {
                                PlayNext();
                            }
                            else
                            {
                                IsPlaying = false;
                                _positionTimer.Stop();
                            }
                        }
                        break;
                }
            }
        });
    }

    public void ToggleFavorite(MusicFile song)
    {
        _playlistService.ToggleFavorite(song);
        OnPropertyChanged(nameof(Favorites));
    }

    public async Task<Playlist> CreateFolderPlaylistAsync(string name, string[] folders, IProgress<int>? progress = null)
    {
        var playlist = await _playlistService.CreateFolderPlaylistAsync(name, progress, folders);
        RefreshPlaylists();
        return playlist;
    }

    public Playlist CreateCustomPlaylist(string name)
    {
        var playlist = _playlistService.CreatePlaylist(name, PlaylistType.Custom);
        RefreshPlaylists();
        return playlist;
    }

    public void DeletePlaylist(Playlist playlist)
    {
        _playlistService.DeletePlaylist(playlist);
        RefreshPlaylists();
    }

    public async Task ExportPlaylistAsync(Playlist playlist, string filePath)
    {
        await _playlistService.ExportPlaylistAsync(playlist, filePath);
    }

    public async Task<Playlist?> ImportPlaylistAsync(string filePath)
    {
        var playlist = await _playlistService.ImportPlaylistAsync(filePath);
        if (playlist != null)
            RefreshPlaylists();
        return playlist;
    }

    public async Task AddFilesToPlaylistAsync(Playlist playlist, string[] files)
    {
        foreach (var file in files)
        {
            var musicFile = await _playlistService.LoadMusicFileAsync(file);
            if (musicFile != null && !playlist.Songs.Contains(musicFile))
            {
                playlist.Songs.Add(musicFile);
            }
        }
        playlist.UpdatedAt = DateTime.Now;
        
        if (CurrentPlaylist == playlist)
            LoadPlaylistSongs();
    }

    public async Task RefreshFolderPlaylistAsync(Playlist playlist)
    {
        if (playlist.Type != PlaylistType.Folder) return;
        
        await _playlistService.RefreshFolderPlaylistAsync(playlist);
        
        if (CurrentPlaylist == playlist)
            LoadPlaylistSongs();
    }

    public void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.None => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.None
        };
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        _autoSaveTimer.Stop();
        _audioEngine.Dispose();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
}
