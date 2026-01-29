using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using MusicPlayer.Models;

namespace MusicPlayer.Services;

/// <summary>
/// 音乐索引服务 - 缓存元数据避免重复读取，大幅提升性能
/// </summary>
public class MusicIndexService
{
    private readonly string _indexFile;
    private ConcurrentDictionary<string, MusicFileIndex> _index = new();
    private bool _isDirty;

    public MusicIndexService(string dataFolder)
    {
        _indexFile = Path.Combine(dataFolder, "music_index.json");
    }

    /// <summary>
    /// 加载索引
    /// </summary>
    public async Task LoadAsync()
    {
        if (File.Exists(_indexFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_indexFile);
                var list = JsonSerializer.Deserialize<List<MusicFileIndex>>(json);
                if (list != null)
                {
                    _index = new ConcurrentDictionary<string, MusicFileIndex>(
                        list.ToDictionary(x => x.FilePath.ToLowerInvariant(), x => x));
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// 保存索引
    /// </summary>
    public async Task SaveAsync()
    {
        if (!_isDirty) return;
        
        try
        {
            var json = JsonSerializer.Serialize(_index.Values.ToList(), 
                new JsonSerializerOptions { WriteIndented = false }); // 紧凑格式节省空间
            await File.WriteAllTextAsync(_indexFile, json);
            _isDirty = false;
        }
        catch { }
    }

    /// <summary>
    /// 获取或创建音乐文件索引（带缓存）
    /// </summary>
    public MusicFile? GetOrCreate(string filePath, Func<string, MusicFile?> factory)
    {
        var key = filePath.ToLowerInvariant();
        
        // 检查缓存是否有效
        if (_index.TryGetValue(key, out var cached))
        {
            // 检查文件是否被修改过
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists && fileInfo.LastWriteTimeUtc == cached.LastModified)
            {
                return cached.ToMusicFile();
            }
        }

        // 缓存未命中或已过期，重新读取
        var musicFile = factory(filePath);
        if (musicFile != null)
        {
            var fileInfo = new FileInfo(filePath);
            var indexEntry = new MusicFileIndex
            {
                FilePath = filePath,
                Title = musicFile.Title,
                Artist = musicFile.Artist,
                Album = musicFile.Album,
                DurationTicks = musicFile.Duration.Ticks,
                LastModified = fileInfo.LastWriteTimeUtc
            };
            _index[key] = indexEntry;
            _isDirty = true;
        }

        return musicFile;
    }

    /// <summary>
    /// 批量预加载（并行处理）
    /// </summary>
    public async Task<List<MusicFile>> BatchLoadAsync(IEnumerable<string> filePaths, 
        Func<string, MusicFile?> factory, IProgress<int>? progress = null)
    {
        var files = filePaths.ToList();
        var results = new ConcurrentBag<MusicFile>();
        var processed = 0;

        await Task.Run(() =>
        {
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                filePath =>
                {
                    var musicFile = GetOrCreate(filePath, factory);
                    if (musicFile != null)
                        results.Add(musicFile);

                    Interlocked.Increment(ref processed);
                    progress?.Report(processed * 100 / files.Count);
                });
        });

        return results.ToList();
    }

    /// <summary>
    /// 清理无效索引（文件已删除）
    /// </summary>
    public void Cleanup()
    {
        var toRemove = _index.Where(kv => !File.Exists(kv.Value.FilePath)).Select(kv => kv.Key).ToList();
        foreach (var key in toRemove)
        {
            _index.TryRemove(key, out _);
            _isDirty = true;
        }
    }

    public int CachedCount => _index.Count;
}

/// <summary>
/// 索引条目（精简存储）
/// </summary>
public class MusicFileIndex
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public long DurationTicks { get; set; }
    public DateTime LastModified { get; set; }

    public MusicFile ToMusicFile() => new()
    {
        FilePath = FilePath,
        Title = Title,
        Artist = Artist,
        Album = Album,
        Duration = TimeSpan.FromTicks(DurationTicks)
    };
}
