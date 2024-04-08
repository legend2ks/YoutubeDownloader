using System.Diagnostics;
using System.Reflection;
using Avalonia;
using CommunityToolkit.Mvvm.Input;

namespace YoutubeApp.ViewModels;

public partial class AboutWindowViewModel
{
    [RelayCommand]
    private static void OpenGithubPage()
    {
        Process.Start(new ProcessStartInfo("https://github.com/legend2ks/YoutubeDownloader")
            { UseShellExecute = true });
    }

    [RelayCommand]
    private static void OpenGithubPageDonateSection()
    {
        Process.Start(new ProcessStartInfo("https://github.com/legend2ks/YoutubeDownloader/#-support")
            { UseShellExecute = true });
    }

    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
    public string AvaloniaVersion { get; } = Assembly.GetAssembly(typeof(Application))!.GetName().Version!.ToString(3);
}