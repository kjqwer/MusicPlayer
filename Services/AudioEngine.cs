using NAudio.Wave;
using NAudio.CoreAudioApi;
using MusicPlayer.Models;
using NAudio.Vorbis;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace MusicPlayer.Services;

/// <summary>
/// HiFi音频播放引擎
/// </summary>
public class AudioEngine : IDisposable
{
    private WasapiOut? _wavePlayer;
    private WaveStream? _reader;
    private VolumeSampleProvider? _volumeProvider;
    private readonly Lock _lockObject = new();
    private bool _disposed;
    private string? _currentFilePath; // 记录当前加载的文件路径，用于重新加载

    public event EventHandler<PlaybackState>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? PlaybackStopped;
    public event EventHandler? PlaybackFailed; // 播放失败事件（设备失效等）

    public bool IsPlaying => _wavePlayer?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _wavePlayer?.PlaybackState == PlaybackState.Paused;
    public TimeSpan CurrentPosition => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan TotalDuration => _reader?.TotalTime ?? TimeSpan.Zero;
    
    private float _volume = 0.7f;
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_volumeProvider != null)
                _volumeProvider.Volume = _volume;
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
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                _reader = ext is ".ogg" or ".oga"
                    ? new VorbisWaveReader(filePath)
                    : new AudioFileReader(filePath);

                var sampleProvider = _reader.ToSampleProvider();
                _volumeProvider = new VolumeSampleProvider(sampleProvider) { Volume = _volume };
                var outputProvider = new SampleToWaveProvider(_volumeProvider);

                // 始终使用WASAPI共享模式，允许其他应用同时播放音频
                var wasapi = new WasapiOut(AudioClientShareMode.Shared, Latency);
                wasapi.PlaybackStopped += OnPlaybackStopped;
                wasapi.Init(outputProvider);
                _wavePlayer = wasapi;
                _currentFilePath = filePath; // 记录当前文件路径
            }
            catch (Exception ex)
            {
                _currentFilePath = null;
                throw new InvalidOperationException($"无法加载音频文件: {filePath}. {ex.Message}", ex);
            }
        }
    }
    
    /// <summary>
    /// 获取当前加载的文件路径
    /// </summary>
    public string? CurrentFilePath => _currentFilePath;

    /// <summary>
    /// 开始播放
    /// </summary>
    /// <returns>是否成功开始播放</returns>
    public bool Play()
    {
        lock (_lockObject)
        {
            if (_wavePlayer == null) return false;
            
            try
            {
                _wavePlayer.Play();
                
                // 检查播放是否真的成功
                // 在设备失效后，Play() 可能不抛异常但状态不会变成 Playing
                if (_wavePlayer.PlaybackState != PlaybackState.Playing)
                {
                    PlaybackFailed?.Invoke(this, EventArgs.Empty);
                    return false;
                }
                
                PlaybackStateChanged?.Invoke(this, PlaybackState.Playing);
                return true;
            }
            catch (Exception)
            {
                // 音频设备可能已失效（如休眠后），通知上层重新加载
                PlaybackFailed?.Invoke(this, EventArgs.Empty);
                return false;
            }
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
            _volumeProvider = null;
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
            
            PlaybackStateChanged?.Invoke(this, PlaybackState.Stopped);
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_lockObject)
        {
            if (_reader != null)
            {
                _reader.CurrentTime = position;
                PositionChanged?.Invoke(this, position);
            }
        }
    }

    public void SeekPercent(double percent)
    {
        if (_reader != null)
        {
            var position = TimeSpan.FromSeconds(_reader.TotalTime.TotalSeconds * percent);
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
