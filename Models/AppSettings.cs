namespace MusicPlayer.Models;

/// <summary>
/// 应用设置
/// </summary>
public class AppSettings
{
    public double Volume { get; set; } = 0.7;
    public bool ShuffleMode { get; set; }
    public RepeatMode RepeatMode { get; set; } = RepeatMode.None;
    public string? LastPlayedSongPath { get; set; }
    public string? LastPlaylistId { get; set; }
    public double LastPosition { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool UseWasapiExclusive { get; set; } = true; // WASAPI独占模式实现HiFi
    public int AudioLatency { get; set; } = 100; // 音频延迟(ms)
    public double WindowWidth { get; set; } = 400;
    public double WindowHeight { get; set; } = 600;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public bool CompactMode { get; set; } // 小屏模式
}

public enum RepeatMode
{
    None,
    All,
    One
}
