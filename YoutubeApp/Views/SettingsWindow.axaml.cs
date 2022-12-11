using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using YoutubeApp.ViewModels;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);
            DataContext = App.Host.Services.GetRequiredService<SettingsWindowViewModel>();
        }
    }
}