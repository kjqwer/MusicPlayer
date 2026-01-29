using System.Collections.ObjectModel;

namespace MusicPlayer.Models;

/// <summary>
/// 播放列表模型
/// </summary>
public class Playlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public PlaylistType Type { get; set; } = PlaylistType.Custom;
    public List<string> SourceFolders { get; set; } = new(); // 文件夹映射
    public List<MusicFile> Songs { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public int SongCount => Songs.Count;
}

public enum PlaylistType
{
    Custom,      // 自定义播放列表
    Folder,      // 文件夹映射
    Favorites,   // 收藏夹
    Recent       // 最近播放
}
