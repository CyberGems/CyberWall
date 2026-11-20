using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CyberWall.UI.Services;

/// <summary>
/// Registers the application-wide shortcuts on the main window. Each shortcut is identified
/// by an id so re-registering one from settings does not disturb the other or the window hook.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    /// <summary>Hotkey that shows and hides the main window.</summary>
    public const int MainHotkeyId = 9101;

    /// <summary>Hotkey that shows and hides the floating traffic widget.</summary>
    public const int WidgetHotkeyId = 9102;

    private const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly Dictionary<int, string> _registered = new();

    private IntPtr _hwnd;
    private HwndSource? _source;

    /// <summary>Raised with the id of the shortcut that was pressed.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>
    /// Registers both shortcuts stored in settings, for example after startup or after a
    /// backup restored different values. Settings are the source of truth here, so a
    /// combination another application owns stays unregistered until the user changes it.
    /// </summary>
    public void RegisterBoth(Window window, string mainHotkey, string widgetHotkey)
    {
        _registered.Clear();
        Register(window, MainHotkeyId, mainHotkey);
        Register(window, WidgetHotkeyId, widgetHotkey);
    }

    /// <summary>
    /// Registers (or replaces) the shortcut identified by <paramref name="id"/>. An empty or
    /// blank string clears that shortcut and is reported as success, which is how settings
    /// represents "no shortcut". When the new combination is already owned by another
    /// application, the previous one is restored and false is returned.
    /// </summary>
    public bool Register(Window window, int id, string hotkeyString)
    {
        if (!EnsureHook(window)) return false;

        _registered.TryGetValue(id, out var previous);
        if (TryRegister(id, hotkeyString))
        {
            _registered[id] = hotkeyString.Trim();
            return true;
        }

        TryRegister(id, previous ?? string.Empty);
        return false;
    }

    public void Dispose()
    {
        foreach (var id in _registered.Keys)
        {
            UnregisterHotkey(id);
        }

        _registered.Clear();

        if (_source != null)
        {
            try
            {
                _source.RemoveHook(HwndHook);
            }
            catch { }
            _source = null;
        }

        _hwnd = IntPtr.Zero;
    }

    private bool TryRegister(int id, string hotkeyString)
    {
        UnregisterHotkey(id);

        if (string.IsNullOrWhiteSpace(hotkeyString)) return true;
        if (!TryParseHotkey(hotkeyString, out var modifiers, out var vk)) return false;

        return RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk);
    }

    private void UnregisterHotkey(int id)
    {
        if (_hwnd == IntPtr.Zero) return;

        try
        {
            UnregisterHotKey(_hwnd, id);
        }
        catch { }
    }

    private bool EnsureHook(Window window)
    {
        if (_source != null && _hwnd != IntPtr.Zero) return true;

        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        if (_hwnd == IntPtr.Zero)
        {
            helper.EnsureHandle();
            _hwnd = helper.Handle;
        }

        if (_hwnd == IntPtr.Zero) return false;

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HwndHook);
        return _source != null;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _registered.ContainsKey(wParam.ToInt32()))
        {
            handled = true;
            HotkeyPressed?.Invoke(wParam.ToInt32());
        }

        return IntPtr.Zero;
    }

    public static bool TryParseHotkey(string? str, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        var hasKey = false;
        var parts = str.Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("control", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_CONTROL;
            else if (trimmed.Equals("alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_ALT;
            else if (trimmed.Equals("shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_SHIFT;
            else if (trimmed.Equals("win", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_WIN;
            else if (!hasKey && TryParseKey(trimmed, out vk))
            {
                hasKey = true;
            }
            else
            {
                return false;
            }
        }

        return modifiers != 0 && hasKey;
    }

    public static ModifierKeys GetModifier(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
        Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
        Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
        Key.LWin or Key.RWin => ModifierKeys.Windows,
        _ => ModifierKeys.None
    };

    public static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (key != Key.None) parts.Add(FormatKey(key));
        return string.Join(" + ", parts);
    }

    /// <summary>Turns a stored value such as "Alt+Shift+F2" into the spaced notation used on screen.</summary>
    public static string FormatDisplay(string? hotkey) =>
        string.IsNullOrWhiteSpace(hotkey)
            ? string.Empty
            : string.Join(
                " + ",
                hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool TryParseKey(string token, out uint vk)
    {
        vk = 0;
        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            vk = (uint)char.ToUpperInvariant(token[0]);
            return true;
        }

        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token.AsSpan(1), out var fNum) && fNum >= 1 && fNum <= 24)
        {
            vk = (uint)(0x70 + (fNum - 1));
            return true;
        }

        var normalized = token.ToUpperInvariant() switch
        {
            "ESC" or "ESCAPE" => "Escape",
            "RETURN" => "Enter",
            "BACKSPACE" => "Back",
            "DEL" => "Delete",
            "PGUP" => "PageUp",
            "PGDN" => "PageDown",
            _ => token
        };

        if (!Enum.TryParse<Key>(normalized, ignoreCase: true, out var key) || key == Key.None)
        {
            return false;
        }

        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }

    private static string FormatKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(CultureInfo.InvariantCulture),
        >= Key.NumPad0 and <= Key.NumPad9 => $"NumPad{(int)key - (int)Key.NumPad0}",
        >= Key.F1 and <= Key.F24 => key.ToString(),
        Key.Escape => "Esc",
        Key.Enter => "Enter",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Insert => "Insert",
        Key.Space => "Space",
        Key.Tab => "Tab",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        _ => key.ToString()
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
