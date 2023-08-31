using YoutubeApp.ViewModels;

namespace YoutubeApp;

public static class DesignViewModels
{
    public static MainWindowViewModel MainWindow => new MainWindowViewModelDesign();
    public static DownloadsViewModel DownloadsView => new DownloadsViewModelDesign();
    public static ChannelsViewModel ChannelsView => new ChannelsViewModelDesign();
    public static JobDetailsWindowViewModel JobDetailsWindow => new JobDetailsWindowViewModelDesign();

    public static ChooseSingleFormatWindowViewModel ChooseSingleFormatWindow =>
        new ChooseSingleFormatWindowViewModelDesign();

    public static FormatWindowViewModel FormatWindow => new FormatWindowViewModelDesign();
    public static AddLinkWindowViewModel AddLinkWindow => new AddLinkWindowViewModelDesign();
    public static LogWindowViewModel LogWindow => new LogWindowViewModelDesign();

    public static ChannelCategorySettingsWindowViewModel ChannelCategorySettingsWindow =>
        new ChannelCategorySettingsWindowViewModelDesign();

    public static MoveChannelWindowViewModelDesign MoveChannelWindow => new MoveChannelWindowViewModelDesign();
}