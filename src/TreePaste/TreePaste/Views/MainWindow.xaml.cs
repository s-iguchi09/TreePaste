using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TreePaste.Infrastructure;
using TreePaste.ViewModels;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace TreePaste.Views;

/// <summary>
/// アプリケーションのメインウィンドウ。タスクトレイとホットキーを管理する。
/// Main window of the application. Manages the system tray icon and global hotkey.
/// </summary>
public partial class MainWindow : Window
{
    private const int HOTKEY_ID = 9000;
    private HwndSource? _hwndSource;
    private NotifyIcon? _notifyIcon;
    private System.Windows.Controls.ContextMenu? _trayContextMenu;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>
    /// TreeView 上のマウスホイールイベントを親の ScrollViewer に転送する。
    /// Forwards mouse wheel events on the TreeView to the parent ScrollViewer.
    /// </summary>
    private void TreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled)
        {
            e.Handled = true;
            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            TreeScrollViewer.RaiseEvent(args);
        }
    }

    /// <summary>
    /// コンストラクター。ウィンドウを初期化し、非表示状態で起動する。
    /// Constructor. Initializes the window and starts in a hidden state.
    /// </summary>
    public MainWindow()
    {
        Visibility = Visibility.Hidden;
        ShowInTaskbar = false;
        WindowState = WindowState.Normal;

        InitializeComponent();

        ViewModel.CloseRequested += (_, _) => HideWindow();
        Closed += MainWindow_Closed;
    }

    /// <summary>
    /// ウィンドウハンドル生成後にホットキーとノティファイアイコンを初期化する。
    /// Initializes the hotkey and notify icon after the window handle is created.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        RegisterHotKey();
        InitializeNotifyIcon();
    }

    // ━━━ タスクトレイ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// タスクトレイアイコンとコンテキストメニューを初期化する。
    /// Initializes the system tray icon and its context menu.
    /// </summary>
    private void InitializeNotifyIcon()
    {
        InitializeTrayContextMenu();

        var iconUri = new Uri("pack://application:,,,/Assets/icon.ico");
        var iconStream = System.Windows.Application.GetResourceStream(iconUri)!.Stream;
        _notifyIcon = new NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Text = "Tree Paste",
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowAndLoadClipboard();
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                Dispatcher.Invoke(() =>
                {
                    var helper = new WindowInteropHelper(this);
                    Win32Api.SetForegroundWindow(helper.Handle);
                    _trayContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    _trayContextMenu.IsOpen = true;
                });
            }
        };
    }

    /// <summary>
    /// タスクトレイのコンテキストメニューを初期化する。
    /// Initializes the context menu for the system tray icon.
    /// </summary>
    private void InitializeTrayContextMenu()
    {
        var showItem = new System.Windows.Controls.MenuItem
        {
            Header = "Show (Ctrl+Alt+V)"
        };
        showItem.Click += (_, _) => ShowAndLoadClipboard();

        var githubItem = new System.Windows.Controls.MenuItem
        {
            Header = "GitHub"
        };
        githubItem.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/s-iguchi09/TreePaste",
                UseShellExecute = true
            });

        var separator = new System.Windows.Controls.Separator();

        var exitItem = new System.Windows.Controls.MenuItem
        {
            Header = "Exit"
        };
        exitItem.Click += (_, _) =>
        {
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        };

        _trayContextMenu = new System.Windows.Controls.ContextMenu
        {
            Items = { showItem, githubItem, separator, exitItem },
            StaysOpen = false
        };
    }

    // ━━━ ホットキー ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// グローバルホットキー（Ctrl+Alt+V）を登録し、ウィンドウメッセージフックを設定する。
    /// Registers the global hotkey (Ctrl+Alt+V) and sets up the window message hook.
    /// </summary>
    private void RegisterHotKey()
    {
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(HwndHook);

        if (!Win32Api.RegisterHotKey(helper.Handle, HOTKEY_ID,
            Win32Api.MOD_CONTROL | Win32Api.MOD_ALT, Win32Api.VK_V))
        {
            MessageBox.Show("Failed to register hotkey (Ctrl+Alt+V).",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// ウィンドウメッセージをフックし、WM_HOTKEY を処理する。
    /// Hooks window messages and handles WM_HOTKEY.
    /// </summary>
    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Api.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            handled = true;
            ShowAndLoadClipboard();
        }
        return IntPtr.Zero;
    }

    // ━━━ クリップボード読み込み・表示 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// ウィンドウを非表示にする。ツリーをクリアして購読を解除する。
    /// Hides the window. Clears the tree and unsubscribes from events.
    /// </summary>
    private void HideWindow()
    {
        ViewModel.OnHide();
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowAndLoadClipboard()
    {
        ViewModel.LoadFromClipboard();

        ShowInTaskbar = true;
        Show();
        Activate();
        Topmost = true;
        Focus();
    }

    // ━━━ キーボード ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// キーダウンイベントを処理する。Escape キーでウィンドウを隠す。
    /// Handles key down events. Hides the window when the Escape key is pressed.
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            HideWindow();
    }

    // ━━━ ウィンドウ終了処理 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// ウィンドウのクローズをキャンセルし、代わりに非表示状態に移行する。
    /// Cancels the window close and transitions to a hidden state instead.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        HideWindow();
    }

    /// <summary>
    /// ウィンドウが閉じられたときにホットキーを解除する。
    /// Unregisters the hotkey when the window is closed.
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        UnregisterHotKey();
    }

    /// <summary>
    /// 登録済みのグローバルホットキーを解除し、メッセージフックを解除する。
    /// Unregisters the global hotkey and removes the message hook.
    /// </summary>
    private void UnregisterHotKey()
    {
        if (_hwndSource != null)
        {
            var helper = new WindowInteropHelper(this);
            Win32Api.UnregisterHotKey(helper.Handle, HOTKEY_ID);
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
    }
}
