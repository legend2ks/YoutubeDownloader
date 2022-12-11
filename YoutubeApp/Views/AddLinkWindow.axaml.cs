using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using YoutubeApp.Enums;
using YoutubeApp.Messages;
using YoutubeApp.ViewUtils;

namespace YoutubeApp.Views;

public partial class AddLinkWindow : Window, IRecipient<OpenFolderPickerMessage>
{
    public AddLinkWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
            Win32.UseImmersiveDarkMode(TryGetPlatformHandle()!.Handle, true);

        WeakReferenceMessenger.Default.RegisterAll(this, (int)MessengerChannel.AddLinkWindow);
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

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnUnloaded(e);
    }
}