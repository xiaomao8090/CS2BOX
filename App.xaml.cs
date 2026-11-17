using System.Windows;
using System.Windows.Threading;
using CS2Box.Services;

namespace CS2Box;

public partial class App : Application
{
    const string MutexName = "CS2Box.SingleInstance";
    const string ShowEventName = "CS2Box.ShowWindow";

    // 必须活到进程结束：一旦被释放，第二个实例就能启动
    static Mutex? _mutex;
    static bool _ownsMutex;
    static EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            // 已经开着一个：叫它把窗口显示出来，自己退出
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as CS2Box.MainWindow)?.ShowFromTray());
        }) { IsBackground = true, Name = "SingleInstanceListener" };
        listener.Start();

        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log(ex.ExceptionObject as Exception, "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, ex) => { Log(ex.Exception, "Task"); ex.SetObserved(); };

        base.OnStartup(e);
        Settings.Load();
        Settings.ApplyTheme();

        // 没同意过（或者换版本了）就先弹免责声明，不同意直接退出
        if (Settings.Current.EulaVersion != Eula.Version)
        {
            var gate = new EulaWindow();
            if (gate.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            Settings.Current.EulaVersion = Eula.Version;
            Settings.Save();
        }

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            Settings.Flush();
            _mutex?.ReleaseMutex();
        }
        base.OnExit(e);
    }

    void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception, "UI");
        Toast.Error($"出错了：{e.Exception.Message}（详情在 error.log）");
        e.Handled = true;
    }

    public static string LogPath => Path.Combine(Settings.DataDir, "error.log");

    static void Log(Exception? ex, string source)
    {
        if (ex == null) return;
        try
        {
            Directory.CreateDirectory(Settings.DataDir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source}) {ex}\n\n");
        }
        catch { /* 连日志都写不了就算了 */ }
    }
}
