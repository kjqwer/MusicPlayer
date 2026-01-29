using System.Text.Json.Serialization;

namespace MusicPlayer.Models;

/// <summary>
/// 音乐文件模型
/// </summary>
public class MusicFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = "未知艺术家";
    public string Album { get; set; } = "未知专辑";
    public TimeSpan Duration { get; set; }
    public bool IsFavorite { get; set; }
    
    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(Title) 
        ? System.IO.Path.GetFileNameWithoutExtension(FilePath) 
        : Title;
    
    [JsonIgnore]
    public string DurationText => Duration.ToString(@"mm\:ss");
    
    [JsonIgnore]
    public bool FileExists => System.IO.File.Exists(FilePath);

    public override bool Equals(object? obj)
    {
        if (obj is MusicFile other)
            return FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public override int GetHashCode() => FilePath.ToLowerInvariant().GetHashCode();
}
