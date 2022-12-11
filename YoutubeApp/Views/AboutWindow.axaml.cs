using Avalonia.Controls;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);
    }
}