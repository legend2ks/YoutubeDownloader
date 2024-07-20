using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using YoutubeApp.Database;
using YoutubeApp.Downloader;
using YoutubeApp.Media;
using YoutubeApp.Messages;
using YoutubeApp.ViewModels;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class MainWindow : Window, IRecipient<GrabberListCloseMessage>, IRecipient<GrabberListIsOpenMessage>,
    IRecipient<ShowMessageBoxMessage>, IRecipient<ShowChooseSingleFormatWindowMessage>,
    IRecipient<ShowJobDetailsWindowMessage>, IRecipient<ShowAddLinkWindowMessage>,
    IRecipient<ShowSettingsWindowMessage>, IRecipient<ShowAboutWindowMessage>,
    IRecipient<ShowChooseFormatWindowMessage>, IRecipient<ShowColumnsWindowMessage>,
    IRecipient<ShowMessageBoxCheckboxMessage>, IRecipient<ShowLogWindowMessage>, IRecipient<OpenFolderPickerMessage>,
    IRecipient<SetClipboardTextMessage>, IRecipient<ShowAddChannelWindowMessage>,
    IRecipient<ShowMoveChannelWindowMessage>, IRecipient<GetActiveTabIndexMessage>,
    IRecipient<ShowVideoInChannelMessage>
{
    public MainWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (e.CloseReason == WindowCloseReason.WindowClosing && e.IsProgrammatic == false)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    public void Receive(ShowChooseSingleFormatWindowMessage message)
    {
        var chooseSingleFormatWindow = new ChooseSingleFormatWindow
        {
            DataContext = new ChooseSingleFormatWindowViewModel(message.Download,
                App.Host.Services.GetRequiredService<DownloadData>(),
                App.Host.Services.GetRequiredService<DownloaderUtils>(),
                App.Host.Services.GetRequiredService<Settings>())
        };
        var result = chooseSingleFormatWindow.ShowDialog<bool>(this);
        message.Reply(result);
    }

    public void Receive(GrabberListCloseMessage message)
    {
        GrabberBtn.Flyout!.Hide();
    }

    public void Receive(GrabberListIsOpenMessage message)
    {
        message.Reply(GrabberBtn.Flyout!.IsOpen);
    }

    public void Receive(ShowMessageBoxMessage message)
    {
        var result = MessageBoxManager
            .GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ContentTitle = message.Title,
                ContentMessage = message.Message,
                ButtonDefinitions = message.ButtonDefinitions,
                Icon = message.Icon,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowIcon = new WindowIcon(AssetLoader.Open(new Uri("avares://YoutubeApp/Assets/app-logo.ico"))),
            }).ShowWindowDialogAsync(this);
        message.Reply(result);
    }

    public void Receive(ShowJobDetailsWindowMessage message)
    {
        var jobDetailsWindow = new JobDetailsWindow
        {
            DataContext = new JobDetailsWindowViewModel(message.Job, WeakReferenceMessenger.Default)
        };
        var result = jobDetailsWindow.ShowDialog<bool>(this);
        message.Reply(result);
    }

    public void Receive(ShowAddLinkWindowMessage message)
    {
        var addLinkWindow = new AddLinkWindow
        {
            DataContext = App.Host.Services.GetRequiredService<AddLinkWindowViewModel>()
        };
        var result = addLinkWindow.ShowDialog<AddLinkWindowResult?>(this);
        message.Reply(result);
    }

    public void Receive(ShowSettingsWindowMessage message)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = App.Host.Services.GetRequiredService<SettingsWindowViewModel>()
        };
        var result = settingsWindow.ShowDialog<bool>(this);
        message.Reply(result);
    }

    public void Receive(ShowAboutWindowMessage message)
    {
        var aboutWindow = new AboutWindow
        {
            DataContext = new AboutWindowViewModel()
        };
        var result = aboutWindow.ShowDialog<bool>(this);
        message.Reply(result);
    }

    public void Receive(ShowChooseFormatWindowMessage message)
    {
        var formatWindow = new FormatWindow
        {
            DataContext = new FormatWindowViewModel(message.SelectedItems,
                App.Host.Services.GetRequiredService<DownloadData>(),
                App.Host.Services.GetRequiredService<DownloaderUtils>(),
                App.Host.Services.GetRequiredService<Settings>())
        };
        var result = formatWindow.ShowDialog<bool>(this);
        message.Reply(result);
    }

    public void Receive(ShowColumnsWindowMessage message)
    {
        var columnsWindow = new ColumnsWindow
        {
            DataContext = App.Host.Services.GetRequiredService<ColumnsWindowViewModel>()
        };
        var result = columnsWindow.ShowDialog<IEnumerable<ColumnConfig>?>(this);
        message.Reply(result);
    }

    public void Receive(ShowMessageBoxCheckboxMessage message)
    {
        var result = MessageBoxManager.GetMessageBoxCheckbox(new MessageBoxCheckboxParams
        {
            ContentTitle = message.Title,
            ContentMessage = message.Message,
            CheckboxText = message.CheckboxText,
            CheckboxDefaultState = message.CheckboxDefaultState,
            Icon = message.Icon,
            ButtonDefinitions = message.ButtonDefinitions,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowIcon = new WindowIcon(AssetLoader.Open(new Uri("avares://YoutubeApp/Assets/app-logo.ico"))),
        }).ShowWindowDialogAsync(this);
        message.Reply(result);
    }

    public void Receive(ShowLogWindowMessage message)
    {
        var logWindow = new LogWindow
        {
            DataContext = new LogWindowViewModel { Title = message.Title, Items = message.Items }
        };
        var result = logWindow.ShowDialog<bool>(this);
        message.Reply(result);
    }

    public async void Receive(OpenFolderPickerMessage message)
    {
        IStorageFolder? suggestedStartLocation = null;
        if (message.SuggestedStartLocation is not null)
            suggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(message.SuggestedStartLocation);
        var result = StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = message.Title,
                SuggestedStartLocation = suggestedStartLocation,
            });
        message.Reply(result!);
    }

    public async void Receive(SetClipboardTextMessage message)
    {
        await Clipboard!.SetTextAsync(message.Text);
    }

    public void Receive(ShowAddChannelWindowMessage message)
    {
        var addPlaylistWindow = new AddChannelWindow
        {
            DataContext = new AddChannelWindowViewModel(
                    App.Host.Services.GetRequiredService<ChannelData>(),
                    App.Host.Services.GetRequiredService<IYoutubeCommunicator>(),
                    App.Host.Services.GetRequiredService<IMessenger>())
                { ChannelCategories = message.ChannelCategories }
        };
        var result = addPlaylistWindow.ShowDialog<AddChannelWindowResult?>(this);
        message.Reply(result);
    }

    public void Receive(ShowMoveChannelWindowMessage message)
    {
        var moveChannelWindow = new MoveChannelWindow
        {
            DataContext = new MoveChannelWindowViewModel(App.Host.Services.GetRequiredService<IMessenger>(),
                App.Host.Services.GetRequiredService<ChannelData>(),
                App.Host.Services.GetRequiredService<DownloadData>(),
                App.Host.Services.GetRequiredService<DownloaderUtils>(),
                message.Channel, message.DestPath)
        };
        var result = moveChannelWindow.ShowDialog<bool>(this);
        message.Reply(result);
    }

    public void Receive(GetActiveTabIndexMessage message)
    {
        message.Reply(Tabs.SelectedIndex);
    }

    public void Receive(ShowVideoInChannelMessage message)
    {
        Tabs.SelectedIndex = 1;
    }
}