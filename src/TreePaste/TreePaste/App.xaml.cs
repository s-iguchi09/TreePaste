using System.Threading;
using System.Windows;
using System.Windows.Interop;
using TreePaste.Services;
using TreePaste.Views;
using Application = System.Windows.Application;

namespace TreePaste
{
    /// <summary>
    /// アプリケーションのエントリポイントとなる App クラス。起動時の初期化処理を担当する。
    /// App class that serves as the application entry point. Handles initialization on startup.
    /// </summary>
    public partial class App : Application
    {
        private MainWindow? _mainWindow;
        private Mutex? _mutex;

        /// <summary>
        /// アプリケーション起動時にメインウィンドウの生成とウォームアップ処理を行う。
        /// Creates the main window and performs warm-up processing on application startup.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mutex = new Mutex(true, "TreePaste_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                Shutdown();
                return;
            }

            _mainWindow = new MainWindow();

            // ウィンドウハンドルを事前生成しておくことで、
            // 初回表示時に一瞬チラつく問題を回避する
            var helper = new WindowInteropHelper(_mainWindow);
            helper.EnsureHandle();

            // COM (Shell.Application) と Windows Forms アセンブリを
            // バックグラウンドで事前初期化し、初回ホットキー時の遅延を削減する
            Task.Run(ExplorerHelper.PreWarm);
        }

        /// <summary>
        /// アプリケーション終了時に Mutex を解放する。
        /// Releases the mutex on application exit.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
