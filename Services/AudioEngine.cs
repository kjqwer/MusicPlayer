using NAudio.Wave;
using NAudio.CoreAudioApi;
using MusicPlayer.Models;

namespace MusicPlayer.Services;

/// <summary>
/// HiFi音频播放引擎
/// </summary>
public class AudioEngine : IDisposable
{
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFileReader;
    private readonly object _lockObject = new();
    private bool _disposed;

    public event EventHandler<PlaybackState>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? PlaybackStopped;

    public bool IsPlaying => _wavePlayer?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _wavePlayer?.PlaybackState == PlaybackState.Paused;
    public TimeSpan CurrentPosition => _audioFileReader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan TotalDuration => _audioFileReader?.TotalTime ?? TimeSpan.Zero;
    
    private float _volume = 0.7f;
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_audioFileReader != null)
                _audioFileReader.Volume = _volume;
        }
    }

    // 延迟缓冲（毫秒）
    public int Latency { get; set; } = 200;

    /// <summary>
    /// 加载并播放音频文件
    /// </summary>
    public void Load(string filePath)
    {
        lock (_lockObject)
        {
            Stop();
            
            try
            {
                _audioFileReader = new AudioFileReader(filePath)
                {
                    Volume = _volume
                };

                // 始终使用WASAPI共享模式，允许其他应用同时播放音频
                var wasapi = new WasapiOut(AudioClientShareMode.Shared, Latency);
                wasapi.PlaybackStopped += OnPlaybackStopped;
                wasapi.Init(_audioFileReader);
                _wavePlayer = wasapi;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法加载音频文件: {filePath}. {ex.Message}", ex);
            }
        }
    }

    public void Play()
    {
        lock (_lockObject)
        {
            _wavePlayer?.Play();
            PlaybackStateChanged?.Invoke(this, PlaybackState.Playing);
        }
    }

    public void Pause()
    {
        lock (_lockObject)
        {
            _wavePlayer?.Pause();
            PlaybackStateChanged?.Invoke(this, PlaybackState.Paused);
        }
    }

    public void Stop()
    {
        lock (_lockObject)
        {
            if (_wavePlayer != null)
            {
                _wavePlayer.Stop();
                _wavePlayer.Dispose();
                _wavePlayer = null;
            }
            
            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
            
            PlaybackStateChanged?.Invoke(this, PlaybackState.Stopped);
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_lockObject)
        {
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = position;
                PositionChanged?.Invoke(this, position);
            }
        }
    }

    public void SeekPercent(double percent)
    {
        if (_audioFileReader != null)
        {
            var position = TimeSpan.FromSeconds(_audioFileReader.TotalTime.TotalSeconds * percent);
            Seek(position);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
