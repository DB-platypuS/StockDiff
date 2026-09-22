// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 应用程序入口，初始化 WinForms 运行时、文件日志与全局异常兜底，再启动主窗体。

using System.Diagnostics;
using StockDiff.Core.Config;

namespace StockDiff.App;

internal static class Program
{
    // 程序入口：初始化 WinForms 与文件日志 → 注册全局异常兜底 → 启动主窗体
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        ConfigureLogging();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception);
        Application.Run(new MainForm());
    }

    // 将 Trace 输出重定向到 %AppData%\kc-stock-diff\logs\app.log（追加写）
    // 日志目录不可写时降级为无文件日志，不影响程序启动
    private static void ConfigureLogging()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppConfig.DataDirName,
                "logs");
            Directory.CreateDirectory(dir);

            var logFile = Path.Combine(dir, AppConfig.LogFileName);
            RotateIfTooLarge(logFile);

            var writer = new StreamWriter(logFile, append: true) { AutoFlush = true };
            Trace.Listeners.Add(new TextWriterTraceListener(writer));
            Trace.AutoFlush = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[启动] 日志文件初始化失败: {ex.Message}");
        }
    }

    // 日志超过上限时轮转：保留上一周期为 app.log.1，旧归档直接覆盖（上限见 AppConfig.LogMaxBytes）
    private static void RotateIfTooLarge(string logFile)
    {
        var info = new FileInfo(logFile);
        if (!info.Exists || info.Length < AppConfig.LogMaxBytes)
        {
            return;
        }

        File.Move(logFile, logFile + ".1", overwrite: true);
    }

    // 记录未捕获异常并提示用户，避免 Release 版崩溃后无日志可查
    private static void ReportFatal(Exception? ex)
    {
        Trace.WriteLine($"[FATAL] {ex}");
        Trace.Flush();
        MessageBox.Show(ex?.Message ?? "发生未知错误", "程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}