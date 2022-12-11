using Avalonia.Controls;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class FormatWindow : Window
{
    public FormatWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);
    }
}