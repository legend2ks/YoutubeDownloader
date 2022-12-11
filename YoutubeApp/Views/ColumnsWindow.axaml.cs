using Avalonia.Controls;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class ColumnsWindow : Window
{
    public ColumnsWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);
    }
}