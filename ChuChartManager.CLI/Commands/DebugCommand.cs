using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class DebugCommand : Command
{
    public override int Execute(CommandContext context)
    {
        Log.EnableConsoleOutput();
        Log.Info("以 debug 模式启动");

        // WebView2 (COM) 要求 STA 线程；CLI 的 async 入口默认在 MTA 中运行，
        // 主项目 csproj 排除了 WPF App.xaml.cs，直接调用 Program.Main() 走 WinForms 入口
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                Program.Main();
            }
            catch (Exception e)
            {
                exception = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            Console.Error.WriteLine($"发生错误: {exception}");
            throw exception;
        }
        return 0;
    }
}
