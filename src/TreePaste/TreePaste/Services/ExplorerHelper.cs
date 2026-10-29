using System.Diagnostics;
using System.Runtime.InteropServices;
using TreePaste.Infrastructure;

namespace TreePaste.Services;

/// <summary>
/// アクティブなエクスプローラーウィンドウのパス取得と COM のウォームアップ処理を提供するユーティリティクラス。
/// Utility class providing active Explorer window path retrieval and COM warm-up processing.
/// </summary>
internal static class ExplorerHelper
{
    /// <summary>
    /// 現在フォアグラウンドにあるエクスプローラーウィンドウのフォルダーパスを取得する。
    /// Retrieves the folder path of the currently active foreground Explorer window.
    /// </summary>
    /// <returns>エクスプローラーのパス。エクスプローラーがアクティブでない場合は null。 / Explorer path, or null if Explorer is not active.</returns>
    public static string? GetActiveExplorerPath()
    {
        IntPtr foregroundWindow = Win32Api.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
            return null;

        Win32Api.GetWindowThreadProcessId(foregroundWindow, out uint processId);

        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                return GetExplorerPathFromWindow(foregroundWindow);
            }
        }
        catch
        {
            // Process might have exited
        }

        return null;
    }

    /// <summary>
    /// 指定ウィンドウハンドルに対応するエクスプローラーのパスを Shell.Application COM 経由で取得する。
    /// Retrieves the Explorer path for the specified window handle via the Shell.Application COM object.
    /// </summary>
    /// <param name="targetHwnd">取得対象のウィンドウハンドル。 / Target window handle.</param>
    /// <returns>パス文字列。取得できない場合はデスクトップパス。 / Path string, or desktop path if not retrievable.</returns>
    private static string? GetExplorerPathFromWindow(IntPtr targetHwnd)
    {
        // Windows 11 のタブでは全タブが同じトップレベル HWND を共有するため、
        // Z オーダー最前面の ShellTabWindowClass（＝アクティブタブ）で照合する。
        // With Windows 11 tabs, all tabs share the same top-level HWND,
        // so match against the topmost ShellTabWindowClass (the active tab).
        IntPtr activeTab = Win32Api.FindWindowEx(targetHwnd, IntPtr.Zero, "ShellTabWindowClass", null);

        try
        {
            Type? shellWindowsType = Type.GetTypeFromProgID("Shell.Application");
            if (shellWindowsType == null)
                return GetDefaultPath();

            dynamic? shell = Activator.CreateInstance(shellWindowsType);
            if (shell == null)
                return GetDefaultPath();

            try
            {
                dynamic windows = shell.Windows();

                foreach (dynamic window in windows)
                {
                    try
                    {
                        if (window.HWND == (int)targetHwnd
                            && (activeTab == IntPtr.Zero || GetTabWindow(window) == activeTab))
                        {
                            string? locationUrl = window.LocationURL as string;
                            if (!string.IsNullOrEmpty(locationUrl))
                            {
                                if (locationUrl.StartsWith("file:///"))
                                {
                                    var path = Uri.UnescapeDataString(locationUrl.Replace("file:///", ""));
                                    return path.Replace('/', '\\');
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip this window
                    }
                    finally
                    {
                        if (window != null)
                            Marshal.ReleaseComObject(window);
                    }
                }

                if (windows != null)
                    Marshal.ReleaseComObject(windows);
            }
            finally
            {
                if (shell != null)
                    Marshal.ReleaseComObject(shell);
            }
        }
        catch (COMException)
        {
            // COM error accessing shell windows
        }

        return GetDefaultPath();
    }

    /// <summary>
    /// シェルウィンドウ（タブ）に対応する ShellTabWindowClass のウィンドウハンドルを取得する。
    /// Retrieves the ShellTabWindowClass window handle for a shell window (tab).
    /// </summary>
    /// <param name="window">Shell.Application.Windows() の要素。 / An item of Shell.Application.Windows().</param>
    /// <returns>タブのウィンドウハンドル。取得できない場合は IntPtr.Zero。 / Tab window handle, or IntPtr.Zero if not retrievable.</returns>
    private static IntPtr GetTabWindow(object window)
    {
        if (window is not IServiceProvider serviceProvider)
            return IntPtr.Zero;

        Guid sidTopLevelBrowser = SID_STopLevelBrowser;
        Guid iidShellBrowser = typeof(IShellBrowser).GUID;
        serviceProvider.QueryService(ref sidTopLevelBrowser, ref iidShellBrowser, out object browserObject);
        try
        {
            ((IShellBrowser)browserObject).GetWindow(out IntPtr hwnd);
            return hwnd;
        }
        finally
        {
            Marshal.ReleaseComObject(browserObject);
        }
    }

    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");

    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        void QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
    }

    /// <summary>
    /// IShellBrowser のうち先頭の IOleWindow::GetWindow のみを宣言したもの。
    /// IShellBrowser declaring only its first method, IOleWindow::GetWindow.
    /// </summary>
    [ComImport]
    [Guid("000214E2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void GetWindow(out IntPtr phwnd);
    }

    /// <summary>
    /// デフォルトのコピー先パス（デスクトップ）を返す。
    /// Returns the default destination path (Desktop).
    /// </summary>
    /// <returns>デスクトップのフルパス。 / Full path to the Desktop.</returns>
    private static string GetDefaultPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    /// <summary>
    /// COM (Shell.Application) を事前初期化してキャッシュさせる。
    /// 初回ホットキー起動時の遅延を削減するため、アプリ起動直後にバックグラウンドで呼ぶ。
    /// Pre-initializes and caches COM (Shell.Application).
    /// Call in the background immediately after app startup to reduce latency on the first hotkey trigger.
    /// </summary>
    public static void PreWarm()
    {
        try
        {
            Type? shellWindowsType = Type.GetTypeFromProgID("Shell.Application");
            if (shellWindowsType == null) return;

            dynamic? shell = Activator.CreateInstance(shellWindowsType);
            if (shell == null) return;

            try
            {
                dynamic windows = shell.Windows();
                if (windows != null)
                    Marshal.ReleaseComObject(windows);
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
        catch
        {
            // ウォームアップ失敗は無視する
        }
    }
}
