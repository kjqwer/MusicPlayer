# HiFi Music Player

简洁、高品质的本地音乐播放器。

## ✨ 功能特性

### 🎧 HiFi音质
- WASAPI独占模式输出，绕过Windows混音器
- 支持 MP3、FLAC、WAV、AAC、APE 等格式

### 📋 播放列表
- 文件夹映射（自动扫描、去重）
- 自定义播放列表
- 收藏夹
- 导入/导出（JSON格式）

### 🎮 播放控制
- 随机播放（智能不重复）
- 循环模式（单曲/列表/无）
- 断点续播（记住上次位置）
- **系统媒体键支持**（耳机、键盘、脚本）

### 🖥️ 界面
- 深色主题
- 小屏模式（右下角悬浮）
- 系统托盘最小化
- 拖放添加文件

## 🚀 运行

```powershell
dotnet run
```

## 📦 构建

```powershell
dotnet build -c Release
```

## ⌨️ 快捷键

| 功能 | 快捷键 |
|------|--------|
| 播放/暂停 | 媒体键 / 空格 |
| 上一首 | 媒体键 |
| 下一首 | 媒体键 |
| 小屏模式 | 双击标题栏 |

## 📁 数据位置

```
%AppData%\HiFiMusicPlayer\
├── settings.json      # 设置
├── playlists.json     # 播放列表
├── favorites.json     # 收藏
└── music_index.json   # 元数据缓存
```

## 🛠️ 技术栈

- .NET 10 + WPF
- NAudio（音频播放）
- Windows.Media.Playback（系统媒体集成）
- TagLibSharp（元数据读取）
