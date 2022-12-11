using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;

namespace YoutubeApp.ViewModels;

public partial class ApplicationViewModel : ViewModelBase
{
    [RelayCommand]
    private static void TrayIconClicked()
    {
        var mainWindow =
            ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
        mainWindow.WindowState = Settings.WindowStateBeforeHide;
        mainWindow.Show();
        mainWindow.Activate();
    }

    [RelayCommand]
    private static void Exit()
    {
        var appLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        appLifetime.TryShutdown();
    }
}