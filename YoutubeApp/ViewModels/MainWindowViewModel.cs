using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia.Enums;
using YoutubeApp.Downloader;
using YoutubeApp.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    public DownloadManager DownloadManager { get; }
    private readonly Settings _settings;
    private readonly IMessenger _messenger;

    public Grabber Grabber { get; protected init; }
    [ObservableProperty] private int _downloadSpeed;

    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }

    private WindowState _windowState;

    public WindowState WindowState
    {
        get => _windowState;
        set
        {
            if (value != WindowState.Minimized)
            {
                Settings.WindowStateBeforeHide = value;
            }

            _windowState = value;
        }
    }

    [ObservableProperty] private bool _downloaderReady;

    private bool _isShuttingDown;

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, Grabber grabber, DownloadManager downloadManager,
        Settings settings, IMessenger messenger)
    {
        _logger = logger;
        _settings = settings;
        _messenger = messenger;
        DownloadManager = downloadManager;
        Grabber = grabber;

        _ = Task.Run(async () =>
        {
            var success = await DownloadManager.InitializeAsync();
            if (success)
            {
                DownloaderReady = true;
            }
            else
            {
                _logger.LogCritical("Downloader initialization failed.");
                Dispatcher.UIThread.Post(() =>
                {
                    _messenger.Send(new ShowMessageBoxMessage
                    {
                        Title = "Downloader Error", Message = "Failed to connect to Aria2",
                        ButtonDefinitions = ButtonEnum.Ok,
                        Icon = Icon.Error
                    }).GetAwaiter().OnCompleted(() =>
                    {
                        var appLifetime =
                            (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                        appLifetime.TryShutdown(-1);
                    });
                });
            }
        });

        WindowWidth = Settings.WindowWidth;
        WindowHeight = Settings.WindowHeight;
        _windowState = Settings.WindowState;

        DownloadManager.ProgressUpdate += DownloadManager_ProgressUpdate;
        DownloadManager.ActiveDownloads.CollectionChanged += ActiveDownloadsOnCollectionChanged;

        var appLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        appLifetime.ShutdownRequested += Application_ShutdownRequested;
    }

    protected MainWindowViewModel()
    {
    }

    private void ActiveDownloadsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isShuttingDown)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var appLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                appLifetime.TryShutdown();
            });
        }

        DownloadSpeed = 0;
    }

    private void Application_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _logger.LogDebug("MainWindowVM - Application shutdown");
        if (DownloadManager.ActiveDownloads.Count > 0)
        {
            e.Cancel = true;
            _isShuttingDown = true;
            DownloaderReady = false;
            return;
        }

        _logger.LogDebug("MainWindowVM - Saving settings");
        _settings.SaveWindowState(WindowWidth, WindowHeight);
    }

    private void DownloadManager_ProgressUpdate(object? sender, ProgressUpdateEventArgs e)
    {
        DownloadSpeed = e.Speed;
    }

    [RelayCommand]
    private async Task AddLinkButtonPressedAsync()
    {
        var result = await _messenger.Send(new ShowAddLinkWindowMessage());
        if (result is null) return;
        Grabber.AddJob(result.Videos, result.Playlists, result.SavePath);
    }

    [RelayCommand]
    private void GrabberJobsButtonPressed()
    {
        Grabber.GrabberHasError = false;
    }

    [RelayCommand]
    private async Task SettingsButtonPressedAsync()
    {
        await _messenger.Send(new ShowSettingsWindowMessage());
    }

    [RelayCommand]
    private async Task AboutButtonPressedAsync()
    {
        await _messenger.Send(new ShowAboutWindowMessage());
    }

    [RelayCommand]
    private static void ExitButtonPressed()
    {
        var appLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        appLifetime.TryShutdown();
    }

    [RelayCommand]
    private async Task JobItemPressedAsync(GrabberJob job)
    {
        Grabber.GrabberJobWindowId = job.Id;
        await _messenger.Send(new ShowJobDetailsWindowMessage { Job = job });
        Grabber.GrabberJobWindowId = null;
    }
}