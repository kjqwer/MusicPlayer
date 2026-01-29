# 全局媒体控制脚本 - 播放/暂停
# 使用Windows API发送系统级媒体控制命令

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class MediaControl {
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();
    
    public const uint WM_APPCOMMAND = 0x0319;
    public const uint APPCOMMAND_MEDIA_PLAY_PAUSE = 14;
    public const uint APPCOMMAND_MEDIA_NEXTTRACK = 11;
    public const uint APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
    public const uint APPCOMMAND_MEDIA_STOP = 13;
    
    public static void SendMediaCommand(uint command) {
        // 只发送到Shell窗口，让系统自动路由到当前活跃的媒体播放器
        // 避免重复发送到具体播放器窗口
        IntPtr shellWindow = GetShellWindow();
        PostMessage(shellWindow, WM_APPCOMMAND, IntPtr.Zero, (IntPtr)(command << 16));
    }
}
"@

function Test-MediaPlayerRunning {
    <#
    .SYNOPSIS
    检测媒体播放器是否正在运行
    
    .DESCRIPTION
    检查常见的媒体播放器进程是否在运行
    
    .PARAMETER Silent
    静默模式，不输出信息
    
    .RETURNS
    bool - 如果媒体播放器正在运行返回true，否则返回false
    #>
    param([bool]$Silent = $false)
    
    # 检查常见的媒体播放器进程
    $mediaPlayers = @(
        "GrooveMusic",
        "WindowsMediaPlayer",
        "QQMusic",
        "Microsoft.Media.Player",
        "PotPlayerMini64"
    )
    
    foreach ($player in $mediaPlayers) {
        $process = Get-Process -Name $player -ErrorAction SilentlyContinue
        if ($process) {
            if (-not $Silent) {
                Write-Host "检测到媒体播放器进程: $player" -ForegroundColor Yellow
            }
            return $true
        }
    }
    
    # 辅助：检测窗口标题
    $found = Get-Process | Where-Object { $_.MainWindowTitle -eq "媒体播放器" }
    if ($found) {
        if (-not $Silent) {
            Write-Host "检测到媒体播放器窗口" -ForegroundColor Yellow
        }
        return $true
    }
    
    return $false
}

function Start-MediaPlayer {
    <#
    .SYNOPSIS
    启动媒体播放器
    
    .DESCRIPTION
    通过快捷方式启动Windows 11媒体播放器
    
    .PARAMETER Silent
    静默模式，不输出信息
    #>
    param([bool]$Silent = $false)
    
    # $shortcutPath = "D:\1awd\快速使用\应用\mus.lnk"
    $shortcutPath = "D:\1awd\快速使用\应用\pot.lnk"
    
    if (Test-Path $shortcutPath) {
        try {
            if (-not $Silent) {
                Write-Host "正在启动媒体播放器..." -ForegroundColor Yellow
            }
            Start-Process $shortcutPath
            if (-not $Silent) {
                Write-Host "媒体播放器已启动" -ForegroundColor Green
            }
            
            # 等待一下让播放器完全启动
            Start-Sleep -Seconds 2
            return $true
        }
        catch {
            Write-Error "启动媒体播放器失败: $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-Error "找不到媒体播放器快捷方式: $shortcutPath"
        return $false
    }
}

function Initialize-MediaPlayer {
    <#
    .SYNOPSIS
    确保媒体播放器正在运行
    
    .DESCRIPTION
    检查媒体播放器是否运行，如果没有运行则启动它
    
    .PARAMETER Silent
    静默模式，不输出信息
    #>
    param([bool]$Silent = $false)
    
    if (-not (Test-MediaPlayerRunning -Silent $Silent)) {
        if (-not $Silent) {
            Write-Host "未检测到媒体播放器运行，正在启动..." -ForegroundColor Yellow
        }
        if (-not (Start-MediaPlayer -Silent $Silent)) {
            if (-not $Silent) {
                Write-Warning "无法启动媒体播放器，但将继续尝试发送媒体控制命令"
            }
        }
    }
}

function Send-PlayPause {
    <#
    .SYNOPSIS
    发送播放/暂停命令到当前活跃的媒体播放器
    
    .DESCRIPTION
    使用Windows API发送系统级播放/暂停命令，适用于所有支持Windows Media Session的播放器
    
    .PARAMETER Silent
    静默模式，不输出信息
    
    .EXAMPLE
    Send-PlayPause
    #>
    param([bool]$Silent = $false)
    
    try {
        # 确保媒体播放器正在运行
        Initialize-MediaPlayer -Silent $Silent
        
        if (-not $Silent) {
            Write-Host "发送播放/暂停命令..." -ForegroundColor Green
        }
        [MediaControl]::SendMediaCommand([MediaControl]::APPCOMMAND_MEDIA_PLAY_PAUSE)
        if (-not $Silent) {
            Write-Host "命令已发送！" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "发送命令时出错: $($_.Exception.Message)"
    }
}

function Send-NextTrack {
    <#
    .SYNOPSIS
    发送下一首命令
    
    .PARAMETER Silent
    静默模式，不输出信息
    
    .EXAMPLE
    Send-NextTrack
    #>
    param([bool]$Silent = $false)
    
    try {
        # 确保媒体播放器正在运行
        Initialize-MediaPlayer -Silent $Silent
        
        if (-not $Silent) {
            Write-Host "发送下一首命令..." -ForegroundColor Green
        }
        [MediaControl]::SendMediaCommand([MediaControl]::APPCOMMAND_MEDIA_NEXTTRACK)
        if (-not $Silent) {
            Write-Host "命令已发送！" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "发送命令时出错: $($_.Exception.Message)"
    }
}

function Send-PreviousTrack {
    <#
    .SYNOPSIS
    发送上一首命令
    
    .PARAMETER Silent
    静默模式，不输出信息
    
    .EXAMPLE
    Send-PreviousTrack
    #>
    param([bool]$Silent = $false)
    
    try {
        # 确保媒体播放器正在运行
        Initialize-MediaPlayer -Silent $Silent
        
        if (-not $Silent) {
            Write-Host "发送上一首命令..." -ForegroundColor Green
        }
        [MediaControl]::SendMediaCommand([MediaControl]::APPCOMMAND_MEDIA_PREVIOUSTRACK)
        if (-not $Silent) {
            Write-Host "命令已发送！" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "发送命令时出错: $($_.Exception.Message)"
    }
}

function Send-Stop {
    <#
    .SYNOPSIS
    发送停止命令
    
    .PARAMETER Silent
    静默模式，不输出信息
    
    .EXAMPLE
    Send-Stop
    #>
    param([bool]$Silent = $false)
    
    try {
        # 确保媒体播放器正在运行
        Initialize-MediaPlayer -Silent $Silent
        
        if (-not $Silent) {
            Write-Host "发送停止命令..." -ForegroundColor Green
        }
        [MediaControl]::SendMediaCommand([MediaControl]::APPCOMMAND_MEDIA_STOP)
        if (-not $Silent) {
            Write-Host "命令已发送！" -ForegroundColor Green
        }
    }
    catch {
        Write-Error "发送命令时出错: $($_.Exception.Message)"
    }
}

# 主程序逻辑
function Main {
    param(
        [Parameter(Position=0)]
        [ValidateSet("play", "pause", "playpause", "next", "prev", "stop", "help")]
        [string]$Action = "playpause",
        
        [Parameter()]
        [switch]$Silent
    )
    
    switch ($Action.ToLower()) {
        "play" { Send-PlayPause -Silent $Silent }
        "pause" { Send-PlayPause -Silent $Silent }
        "playpause" { Send-PlayPause -Silent $Silent }
        "next" { Send-NextTrack -Silent $Silent }
        "prev" { Send-PreviousTrack -Silent $Silent }
        "stop" { Send-Stop -Silent $Silent }
        "help" { 
            Write-Host @"
全局媒体控制脚本

用法:
    .\MediaControl.ps1 [命令] [-Silent]

可用命令:
    playpause  - 播放/暂停 (默认)
    play       - 播放/暂停
    pause      - 播放/暂停
    next       - 下一首
    prev       - 上一首
    stop       - 停止
    help       - 显示此帮助

参数:
    -Silent    - 静默模式，不显示输出信息

示例:
    .\MediaControl.ps1              # 播放/暂停
    .\MediaControl.ps1 next         # 下一首
    .\MediaControl.ps1 prev         # 上一首
    .\MediaControl.ps1 stop         # 停止
    .\MediaControl.ps1 -Silent      # 静默播放/暂停

功能特性:
    - 自动检测媒体播放器是否运行
    - 如果播放器未运行，自动启动Windows 11媒体播放器
    - 适用于所有支持Windows Media Session的播放器
    - 支持静默模式运行

注意: 此脚本适用于所有支持Windows Media Session的播放器
"@ -ForegroundColor Cyan
        }
    }
}

# 如果脚本被直接运行（不是被导入），则执行主程序
if ($MyInvocation.InvocationName -ne '.') {
    # 默认使用静默模式
    Main @args -Silent
} 