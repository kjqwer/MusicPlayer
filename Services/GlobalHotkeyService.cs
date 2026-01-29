using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MusicPlayer.Services;

/// <summary>
/// 全局媒体按键服务 - 通过窗口消息接收媒体控制命令
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private bool _disposed;

    public event EventHandler? PlayPausePressed;
    public event EventHandler? NextPressed;
    public event EventHandler? PreviousPressed;
    public event EventHandler? StopPressed;

    // Windows消息常量
    private const int WM_APPCOMMAND = 0x0319;
    
    // 媒体按键命令 (APPCOMMAND values)
    private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
    private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
    private const int APPCOMMAND_MEDIA_STOP = 13;
    private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;
    private const int APPCOMMAND_MEDIA_PLAY = 46;
    private const int APPCOMMAND_MEDIA_PAUSE = 47;

    // 用于提取APPCOMMAND的掩码
    private const int FAPPCOMMAND_MASK = 0xF000;

    public void Register(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_APPCOMMAND)
        {
            // 从lParam提取命令：高16位中的低12位
            int cmd = (short)(((ushort)((long)lParam >> 16)) & ~FAPPCOMMAND_MASK);
            
            switch (cmd)
            {
                case APPCOMMAND_MEDIA_PLAY_PAUSE:
                case APPCOMMAND_MEDIA_PLAY:
                case APPCOMMAND_MEDIA_PAUSE:
                    PlayPausePressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
                case APPCOMMAND_MEDIA_NEXTTRACK:
                    NextPressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
                case APPCOMMAND_MEDIA_PREVIOUSTRACK:
                    PreviousPressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
                case APPCOMMAND_MEDIA_STOP:
                    StopPressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
            }
            
            // 返回TRUE表示我们处理了这个消息
            if (handled)
                return new IntPtr(1);
        }
        
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source?.RemoveHook(WndProc);
        GC.SuppressFinalize(this);
    }
}
