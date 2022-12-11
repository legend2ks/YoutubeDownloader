using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using YoutubeApp.Database;
using YoutubeApp.Downloader;
using YoutubeApp.Media;
using YoutubeApp.ViewModels;
using YoutubeApp.Views;

namespace YoutubeApp;

public class App : Application
{
    public static IHost Host;

    public App()
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<DownloadsViewModel>();
                services.AddSingleton<ChannelsViewModel>();
                services.AddTransient<SettingsWindowViewModel>();
                services.AddTransient<AddLinkWindowViewModel>();
                services.AddTransient<ColumnsWindowViewModel>();
                services.AddSingleton<SqliteDbAccessProvider>();
                services.AddSingleton<SettingsData>();
                services.AddSingleton<DownloadData>();
                services.AddSingleton<ChannelData>();
                services.AddSingleton<IMessenger>(provider => WeakReferenceMessenger.Default);
                services.AddSingleton<Settings>();
                services.AddSingleton<Grabber>();
                services.AddSingleton<Youtube>();
                services.AddSingleton<DownloadManager>();
                services.AddSingleton<DownloaderUtils>();
                services.AddSingleton<IYoutubeCommunicator, Ytdlp>();
                services.AddSingleton<IAria2, Aria2>();
                services.AddSingleton<Ffmpeg>();
            })
            .Build();
        Host.Start();

        DataContext = new ApplicationViewModel();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            var mainWindow = Host.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Host.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}