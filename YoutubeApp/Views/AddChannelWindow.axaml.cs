using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using YoutubeApp.Enums;
using YoutubeApp.Messages;
using YoutubeApp.ViewModels;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class AddChannelWindow : Window, IRecipient<OpenFolderPickerMessage>, IRecipient<ShowMessageBoxMessage>,
    IRecipient<CloseWindowMessage<AddChannelWindowResult>>
{
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Link.Focus();
    }

    public AddChannelWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);

        WeakReferenceMessenger.Default.RegisterAll(this, (int)MessengerChannel.AddChannelWindow);
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

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnUnloaded(e);
    }

    public void Receive(CloseWindowMessage<AddChannelWindowResult> message)
    {
        Close(message.Value);
    }
}