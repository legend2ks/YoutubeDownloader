using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Messaging;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using YoutubeApp.Enums;
using YoutubeApp.Messages;
using YoutubeApp.ViewModels;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class MoveChannelWindow : Window, IRecipient<ShowMessageBoxCustomMessage>,
    IRecipient<ShowMessageBoxMessage>
{
    public MoveChannelWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);

        WeakReferenceMessenger.Default.RegisterAll(this, (int)MessengerChannel.MoveChannelWindow);
    }

    public void Receive(ShowMessageBoxCustomMessage message)
    {
        var result = MessageBoxManager
            .GetMessageBoxCustomWindow(new MessageBoxCustomParams
            {
                ContentTitle = message.Title,
                ContentMessage = message.Message,
                ButtonDefinitions = message.ButtonDefinitions,
                Icon = message.Icon,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowIcon = new WindowIcon(AssetLoader.Open(new Uri("avares://YoutubeApp/Assets/app-logo.ico"))),
            }).ShowDialog(this);
        message.Reply(result);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var vm = (MoveChannelWindowViewModel)DataContext!;
        vm.CancelCommand.Execute(null);

        base.OnClosing(e);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var vm = (MoveChannelWindowViewModel)DataContext!;
        _ = vm.MoveFilesAsync();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnUnloaded(e);
    }

    public void Receive(ShowMessageBoxMessage message)
    {
        var result = MessageBoxManager
            .GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ContentTitle = message.Title,
                ContentMessage = message.Message,
                ButtonDefinitions = message.ButtonDefinitions,
                Icon = message.Icon,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowIcon = new WindowIcon(AssetLoader.Open(new Uri("avares://YoutubeApp/Assets/app-logo.ico"))),
            }).ShowDialog(this);
        message.Reply(result);
    }
}