using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;

namespace DynamicIslandWin
{
    /// <summary>进程级异常日志：与 config.json 同目录写入 error.log，文件超限自动轮换。任何失败都不向外抛。 </summary>
    internal static class AppLog
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        private const long MaxBytes = 512 * 1024;

        public static void Write(Exception? ex, string label = "unhandled")
        {
            if (ex == null) return;
            try
            {
                var sb = new StringBuilder();
                sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ").Append(label).AppendLine();
                sb.AppendLine(ex.ToString());
                sb.AppendLine("----------------------------------------");

                if (File.Exists(LogPath))
                {
                    try
                    {
                        if (new FileInfo(LogPath).Length > MaxBytes) File.Delete(LogPath);
                    }
                    catch { }
                }
                File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static void Info(string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    '[' + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] [INFO] " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    public partial class App : Application
    {
        private Mutex? _singleInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 拦截 UI 线程未捕获异常：记录日志后标记已处理，避免闪退（不再静默丢弃，方便排障）
            DispatcherUnhandledException += (s, args) =>
            {
                AppLog.Write(args.Exception, "ui-thread");
                args.Handled = true;
            };

            // 拦截非 UI 线程后台异常：同样落盘
            AppDomain.CurrentDomain.UnhandledException += (s, args) => AppLog.Write(args.ExceptionObject as Exception, "appdomain");

            // 单实例保护：重复启动时提示并退出，避免叠出两个置顶小岛
            _singleInstance = new Mutex(true, "DynamicIslandWin_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                try { MessageBox.Show("灵动岛已在运行中。", "DynamicIslandWin", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
                Shutdown();
                return;
            }

            try { new MainWindow().Show(); } catch (Exception ex) { AppLog.Write(ex, "startup"); throw; }

            // 诊断入口：dsh-island.exe --settings 启动后直接打开设置窗（自动化冒烟用）
            try
            {
                if (Array.Exists(e.Args, a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase)) && MainWindow is MainWindow main)
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(main.OpenSettingsWindow));
                }
            }
            catch (Exception ex) { AppLog.Write(ex, "auto-settings"); }
        }
    }
}
