// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 应用程序入口，初始化 WinForms 运行时环境并启动主窗体。

namespace StockDiff.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}