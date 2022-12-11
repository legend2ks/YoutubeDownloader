using Avalonia.Controls;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);
    }
}