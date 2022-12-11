using Avalonia.Controls;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class ChannelCategorySettingsWindow : Window
{
    public ChannelCategorySettingsWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);
    }
}