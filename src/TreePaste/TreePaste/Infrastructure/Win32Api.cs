using System.Runtime.InteropServices;

namespace TreePaste.Infrastructure;

/// <summary>
/// ホットキー登録およびウィンドウ操作に必要な Win32 API を提供するユーティリティクラス。
/// Utility class providing Win32 API calls required for hotkey registration and window operations.
/// </summary>
internal static class Win32Api
{
    /// <summary>
    /// システムワイドのホットキーを登録する。
    /// Registers a system-wide hotkey.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// 登録したホットキーを解除する。
    /// Unregisters a previously registered hotkey.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// 現在のフォアグラウンドウィンドウのハンドルを取得する。
    /// Retrieves the handle of the currently active foreground window.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 指定ウィンドウを作成したスレッドとプロセス ID を取得する。
    /// Retrieves the thread and process ID of the specified window.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // Modifier keys
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // Virtual Key Codes
    public const uint VK_V = 0x56;

    // Windows Messages
    public const int WM_HOTKEY = 0x0312;
}
