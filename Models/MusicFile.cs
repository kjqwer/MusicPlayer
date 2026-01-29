using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MusicPlayer.Models;

/// <summary>
/// 音乐文件模型
/// </summary>
public class MusicFile : INotifyPropertyChanged
{
    private bool _isFavorite;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = "未知艺术家";
    public string Album { get; set; } = "未知专辑";
    public TimeSpan Duration { get; set; }
    
    public bool IsFavorite 
    { 
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();
            }
        }
    }
    
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    
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
