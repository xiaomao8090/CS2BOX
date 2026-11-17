using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CS2Box.Services;

/// <summary>全局热键：在任何地方按下都能启动 CS2。</summary>
public static class Hotkeys
{
    const int Id = 0xC520;
    const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;

    /// <summary>可选的热键：显示名 → (修饰键, 虚拟键码)。</summary>
    public static readonly (string Key, string Name, uint Mods, uint Vk)[] Options =
    [
        ("None", "不使用", 0, 0),
        ("CtrlAltC", "Ctrl + Alt + C", MOD_CONTROL | MOD_ALT, 0x43),
        ("CtrlAltS", "Ctrl + Alt + S", MOD_CONTROL | MOD_ALT, 0x53),
        ("CtrlShiftF12", "Ctrl + Shift + F12", MOD_CONTROL | MOD_SHIFT, 0x7B),
        ("AltF10", "Alt + F10", MOD_ALT, 0x79),
    ];

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    static HwndSource? _source;
    static Action? _onPress;

    public static void Attach(IntPtr hwnd, Action onPress)
    {
        _onPress = onPress;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(Hook);
        Apply(Settings.Current.Hotkey);
    }

    /// <summary>注册设置里选的热键；返回 false 表示被别的程序占用了。</summary>
    public static bool Apply(string key)
    {
        if (_source == null) return false;
        UnregisterHotKey(_source.Handle, Id);
        var opt = Options.FirstOrDefault(o => o.Key == key);
        if (opt.Vk == 0) return true;
        return RegisterHotKey(_source.Handle, Id, opt.Mods | MOD_NOREPEAT, opt.Vk);
    }

    public static void Detach()
    {
        if (_source == null) return;
        UnregisterHotKey(_source.Handle, Id);
        _source.RemoveHook(Hook);
        _source = null;
    }

    static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == Id)
        {
            _onPress?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
