using System;
using System.Windows.Interop;

namespace AdhdScheduler.App.Interop;

/// <summary>Ctrl+Shift+A（固定）のグローバルホットキー。</summary>
internal sealed class GlobalHotKey : IDisposable
{
    private const int HotKeyId = 0xA001;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly Action _onPressed;
    private readonly HwndSource _source;
    private bool _registered;

    public GlobalHotKey(Action onPressed)
    {
        _onPressed = onPressed;
        _source = new HwndSource(new HwndSourceParameters("AdhdSchedulerHotKey")
        {
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE,
        });
        _source.AddHook(WndProc);
    }

    public bool Register()
    {
        _registered = NativeMethods.RegisterHotKey(
            _source.Handle,
            HotKeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_A);
        return _registered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotKeyId)
        {
            _onPressed();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotKeyId);
            _registered = false;
        }
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
