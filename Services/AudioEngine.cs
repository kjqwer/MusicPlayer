using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.CoreAudioApi;
using MusicPlayer.Models;

namespace MusicPlayer.Services;

/// <summary>
/// HiFi音频播放引擎 - 使用WASAPI实现高音质，带重采样和音频处理
/// </summary>
public class AudioEngine : IDisposable
{
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFileReader;
    private ISampleProvider? _sampleProvider;
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

    // 默认使用共享模式，允许其他应用同时播放音频
    public bool UseWasapiExclusive { get; set; } = false;
    // 增加延迟缓冲，减少爆音和尖锐感
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

                // 创建音频处理链：重采样到 48kHz 以获得更平滑的音质
                _sampleProvider = _audioFileReader;
                
                // 如果采样率不是 48000，进行重采样以减少尖锐感
                if (_audioFileReader.WaveFormat.SampleRate != 48000)
                {
                    var resampler = new WdlResamplingSampleProvider(_audioFileReader, 48000);
                    _sampleProvider = resampler;
                }

                // 使用WASAPI输出
                var shareMode = UseWasapiExclusive ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
                try
                {
                    var wasapi = new WasapiOut(shareMode, Latency);
                    wasapi.PlaybackStopped += OnPlaybackStopped;
                    wasapi.Init(_sampleProvider.ToWaveProvider());
                    _wavePlayer = wasapi;
                }
                catch
                {
                    // 如果失败，回退到共享模式
                    var wasapi = new WasapiOut(AudioClientShareMode.Shared, Latency);
                    wasapi.PlaybackStopped += OnPlaybackStopped;
                    wasapi.Init(_sampleProvider.ToWaveProvider());
                    _wavePlayer = wasapi;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法加载音频文件: {ex.Message}", ex);
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
