using System.Windows.Threading;
using MusicPlayer.Models;
using Windows.Media;
using Windows.Media.Playback;

namespace MusicPlayer.Services;

/// <summary>
/// 媒体会话服务 - 使用 Windows.Media.Playback.MediaPlayer 创建系统媒体会话
/// 这是Chrome、Spotify等应用使用的方式，可以接收系统媒体控制命令
/// </summary>
public class MediaSessionService : IDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly SystemMediaTransportControls _smtc;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public event EventHandler? PlayPausePressed;
    public event EventHandler? NextPressed;
    public event EventHandler? PreviousPressed;
    public event EventHandler? StopPressed;

    public MediaSessionService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        
        // 创建MediaPlayer实例 - 这会自动创建一个系统媒体会话
        _mediaPlayer = new MediaPlayer();
        
        // 禁用MediaPlayer的音频输出，我们只用它来获取SMTC控制
        _mediaPlayer.Volume = 0;
        _mediaPlayer.IsMuted = true;
        _mediaPlayer.AutoPlay = false;
        
        // 获取与此MediaPlayer关联的SMTC
        _smtc = _mediaPlayer.SystemMediaTransportControls;
        
        // 启用控制按钮
        _smtc.IsEnabled = true;
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.IsNextEnabled = true;
        _smtc.IsPreviousEnabled = true;
        _smtc.IsStopEnabled = true;
        
        // 设置初始状态
        _smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
        
        // 监听按钮事件
        _smtc.ButtonPressed += Smtc_ButtonPressed;
    }

    /// <summary>
    /// 启动媒体会话（确保SMTC可见）
    /// </summary>
    public void Start()
    {
        // MediaPlayer创建时会自动注册，不需要额外操作
        _smtc.IsEnabled = true;
    }

    /// <summary>
    /// 更新播放状态
    /// </summary>
    public void UpdatePlaybackStatus(bool isPlaying)
    {
        _smtc.PlaybackStatus = isPlaying 
            ? MediaPlaybackStatus.Playing 
            : MediaPlaybackStatus.Paused;
    }

    /// <summary>
    /// 更新当前歌曲信息（显示在系统媒体浮层中）
    /// </summary>
    public void UpdateMetadata(MusicFile? song)
    {
        var updater = _smtc.DisplayUpdater;
        
        if (song == null)
        {
            updater.ClearAll();
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = "HiFi Music Player";
            updater.MusicProperties.Artist = "";
            updater.Update();
            _smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
            return;
        }

        updater.Type = MediaPlaybackType.Music;
        updater.MusicProperties.Title = song.DisplayName;
        updater.MusicProperties.Artist = song.Artist;
        updater.MusicProperties.AlbumTitle = song.Album;
        updater.Update();
    }

    private void Smtc_ButtonPressed(SystemMediaTransportControls sender, 
        SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        _dispatcher.BeginInvoke(() =>
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                case SystemMediaTransportControlsButton.Pause:
                    PlayPausePressed?.Invoke(this, EventArgs.Empty);
                    break;
                case SystemMediaTransportControlsButton.Next:
                    NextPressed?.Invoke(this, EventArgs.Empty);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    PreviousPressed?.Invoke(this, EventArgs.Empty);
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    StopPressed?.Invoke(this, EventArgs.Empty);
                    break;
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _smtc.ButtonPressed -= Smtc_ButtonPressed;
        _smtc.IsEnabled = false;
        _mediaPlayer.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
