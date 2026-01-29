using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using MusicPlayer.Models;
using TagLib;

namespace MusicPlayer.Services;

/// <summary>
/// 播放列表服务 - 管理所有播放列表和音乐文件（已优化性能）
/// </summary>
public class PlaylistService
{
    private readonly string _dataFolder;
    private readonly string _playlistsFile;
    private readonly string _favoritesFile;
    private readonly string _settingsFile;
    private readonly MusicIndexService _indexService;
    
    private static readonly string[] SupportedExtensions = 
        { ".mp3", ".flac", ".wav", ".aac", ".m4a", ".ogg", ".wma", ".aiff", ".ape" };
    
    private static readonly HashSet<string> SupportedExtensionsSet = 
        new(SupportedExtensions, StringComparer.OrdinalIgnoreCase);

    public List<Playlist> Playlists { get; private set; } = new();
    public Playlist Favorites { get; private set; } = new() { Name = "收藏", Type = PlaylistType.Favorites };
    public AppSettings Settings { get; private set; } = new();
    
    /// <summary>
    /// 索引缓存命中数
    /// </summary>
    public int CachedFilesCount => _indexService.CachedCount;

    public PlaylistService()
    {
        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HiFiMusicPlayer");
        
        Directory.CreateDirectory(_dataFolder);
        
        _playlistsFile = Path.Combine(_dataFolder, "playlists.json");
        _favoritesFile = Path.Combine(_dataFolder, "favorites.json");
        _settingsFile = Path.Combine(_dataFolder, "settings.json");
        _indexService = new MusicIndexService(_dataFolder);
    }

    /// <summary>
    /// 加载所有数据
    /// </summary>
    public async Task LoadAsync()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        
        // 并行加载所有数据
        var tasks = new List<Task>();
        
        // 加载索引
        tasks.Add(_indexService.LoadAsync());
        
        // 加载设置
        if (System.IO.File.Exists(_settingsFile))
        {
            tasks.Add(Task.Run(async () =>
            {
                var json = await System.IO.File.ReadAllTextAsync(_settingsFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
            }));
        }
        
        // 加载播放列表
        if (System.IO.File.Exists(_playlistsFile))
        {
            tasks.Add(Task.Run(async () =>
            {
                var json = await System.IO.File.ReadAllTextAsync(_playlistsFile);
                Playlists = JsonSerializer.Deserialize<List<Playlist>>(json, options) ?? new List<Playlist>();
            }));
        }
        
        // 加载收藏
        if (System.IO.File.Exists(_favoritesFile))
        {
            tasks.Add(Task.Run(async () =>
            {
                var json = await System.IO.File.ReadAllTextAsync(_favoritesFile);
                Favorites = JsonSerializer.Deserialize<Playlist>(json, options) 
                    ?? new Playlist { Name = "收藏", Type = PlaylistType.Favorites };
            }));
        }

        await Task.WhenAll(tasks);
        
        // 后台刷新文件夹类型的播放列表（不阻塞启动）
        _ = Task.Run(async () =>
        {
            foreach (var playlist in Playlists.Where(p => p.Type == PlaylistType.Folder))
            {
                await RefreshFolderPlaylistAsync(playlist);
            }
            // 保存更新的索引
            await _indexService.SaveAsync();
        });
    }

    /// <summary>
    /// 保存所有数据
    /// </summary>
    public async Task SaveAsync()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        
        await Task.WhenAll(
            System.IO.File.WriteAllTextAsync(_settingsFile, JsonSerializer.Serialize(Settings, options)),
            System.IO.File.WriteAllTextAsync(_playlistsFile, JsonSerializer.Serialize(Playlists, options)),
            System.IO.File.WriteAllTextAsync(_favoritesFile, JsonSerializer.Serialize(Favorites, options)),
            _indexService.SaveAsync()
        );
    }

    /// <summary>
    /// 创建新的自定义播放列表
    /// </summary>
    public Playlist CreatePlaylist(string name, PlaylistType type = PlaylistType.Custom)
    {
        var playlist = new Playlist { Name = name, Type = type };
        Playlists.Add(playlist);
        return playlist;
    }

    /// <summary>
    /// 从文件夹创建播放列表
    /// </summary>
    public async Task<Playlist> CreateFolderPlaylistAsync(string name, IProgress<int>? progress = null, params string[] folders)
    {
        var playlist = new Playlist
        {
            Name = name,
            Type = PlaylistType.Folder,
            SourceFolders = folders.ToList()
        };
        
        await RefreshFolderPlaylistAsync(playlist, progress);
        Playlists.Add(playlist);
        return playlist;
    }

    /// <summary>
    /// 刷新文件夹播放列表（优化版：并行+缓存）
    /// </summary>
    public async Task RefreshFolderPlaylistAsync(Playlist playlist, IProgress<int>? progress = null)
    {
        if (playlist.Type != PlaylistType.Folder) return;

        // 快速收集所有文件路径
        var allFiles = new List<string>();
        foreach (var folder in playlist.SourceFolders)
        {
            if (!Directory.Exists(folder)) continue;
            
            try
            {
                // 使用EnumerateFiles替代GetFiles，更省内存
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(f => SupportedExtensionsSet.Contains(Path.GetExtension(f)));
                allFiles.AddRange(files);
            }
            catch { } // 忽略无权限的目录
        }

        // 去重
        var uniqueFiles = allFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // 并行批量加载（利用索引缓存）
        var songs = await _indexService.BatchLoadAsync(uniqueFiles, LoadMusicFileSync, progress);

        // 标记收藏状态
        var favoriteSet = new HashSet<string>(
            Favorites.Songs.Select(s => s.FilePath), 
            StringComparer.OrdinalIgnoreCase);
        
        foreach (var song in songs)
        {
            song.IsFavorite = favoriteSet.Contains(song.FilePath);
        }

        playlist.Songs = songs;
        playlist.UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 同步加载音乐文件元数据（供并行调用）
    /// </summary>
    private MusicFile? LoadMusicFileSync(string filePath)
    {
        try
        {
            var musicFile = new MusicFile { FilePath = filePath };
            
            using var tagFile = TagLib.File.Create(filePath);
            musicFile.Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath);
            musicFile.Artist = tagFile.Tag.FirstPerformer ?? "未知艺术家";
            musicFile.Album = tagFile.Tag.Album ?? "未知专辑";
            musicFile.Duration = tagFile.Properties.Duration;
            
            return musicFile;
        }
        catch
        {
            return new MusicFile 
            { 
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath)
            };
        }
    }

    /// <summary>
    /// 加载音乐文件元数据（异步版本，使用缓存）
    /// </summary>
    public Task<MusicFile?> LoadMusicFileAsync(string filePath)
    {
        return Task.Run(() => _indexService.GetOrCreate(filePath, LoadMusicFileSync));
    }

    /// <summary>
    /// 添加到收藏
    /// </summary>
    public void AddToFavorites(MusicFile song)
    {
        if (!Favorites.Songs.Contains(song))
        {
            song.IsFavorite = true;
            Favorites.Songs.Add(song);
            Favorites.UpdatedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 从收藏移除
    /// </summary>
    public void RemoveFromFavorites(MusicFile song)
    {
        song.IsFavorite = false;
        Favorites.Songs.RemoveAll(s => 
            s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
        Favorites.UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 切换收藏状态
    /// </summary>
    public bool ToggleFavorite(MusicFile song)
    {
        if (song.IsFavorite)
        {
            RemoveFromFavorites(song);
            return false;
        }
        else
        {
            AddToFavorites(song);
            return true;
        }
    }

    /// <summary>
    /// 导出播放列表
    /// </summary>
    public async Task ExportPlaylistAsync(Playlist playlist, string filePath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(playlist, options);
        await System.IO.File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// 导入播放列表
    /// </summary>
    public async Task<Playlist?> ImportPlaylistAsync(string filePath)
    {
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var playlist = JsonSerializer.Deserialize<Playlist>(json);
            
            if (playlist != null)
            {
                playlist.Id = Guid.NewGuid().ToString();
                Playlists.Add(playlist);
                return playlist;
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// 删除播放列表
    /// </summary>
    public void DeletePlaylist(Playlist playlist)
    {
        Playlists.Remove(playlist);
    }

    /// <summary>
    /// 清理无效索引
    /// </summary>
    public void CleanupIndex()
    {
        _indexService.Cleanup();
    }
}
