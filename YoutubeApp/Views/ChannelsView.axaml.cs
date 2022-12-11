using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using YoutubeApp.ViewModels;

namespace YoutubeApp.Views;

public partial class ChannelsView : UserControl
{
    public ChannelsView()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            DataContext = App.Host.Services.GetRequiredService<ChannelsViewModel>();
    }
}