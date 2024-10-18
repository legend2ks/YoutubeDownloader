using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using YoutubeApp.ViewModels;

namespace YoutubeApp.Views;

public partial class DownloadsView : UserControl
{
    public DownloadsView()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            DataContext = App.Host.Services.GetRequiredService<DownloadsViewModel>();
    }

    private void HeaderOnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta.X == 0) return;
        DownloadList.Scroll!.Offset = DownloadList.Scroll!.Offset.WithX(HeaderSV.Offset.X);
    }

    private void DownloadListOnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta.X == 0) return;
        HeaderSV.Offset = HeaderSV.Offset.WithX(DownloadList.Scroll!.Offset.X);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        ((ScrollViewer)DownloadList.Scroll!).ScrollChanged += DownloadListOnScrollChanged;
        HeaderSV.ScrollChanged += HeaderOnScrollChanged;
        HotKeyManager.SetHotKey(RemoveButton, new KeyGesture(Key.Delete));
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        HotKeyManager.SetHotKey(RemoveButton, null!);
    }
}