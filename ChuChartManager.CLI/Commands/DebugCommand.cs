using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class DebugCommand : Command
{
    public override int Execute(CommandContext context)
    {
        Log.EnableConsoleOutput();
        Log.Info("以 debug 模式启动");

        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
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
