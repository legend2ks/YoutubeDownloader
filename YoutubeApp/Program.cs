using System;
using System.Reflection;
using System.Threading;
using Avalonia;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Templates;

namespace YoutubeApp;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var mutex = new Mutex(true, "{F6B0828B-C674-4E45-A7CE-E8C9380C8960}", out var createdNew);
        if (!createdNew) return;

        SetupLogger();

        try
        {
            Log.Information("Application starting up, v{Version}",
                Assembly.GetExecutingAssembly().GetName().Version!.ToString(3));
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Application Main");
#if DEBUG
            throw;
#endif
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }

    private static void SetupLogger()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false).Build();
        var expressionTemplate =
            new ExpressionTemplate(
                "[{@t:yyyy-MM-dd HH:mm:ss} {@l:u3} {Coalesce(Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1), '<none>')}] {@m}\n{@x}");
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.Debug(expressionTemplate)
            .WriteTo.File(expressionTemplate, "log.txt", rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 2, fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true)
            .CreateLogger();
    }
}