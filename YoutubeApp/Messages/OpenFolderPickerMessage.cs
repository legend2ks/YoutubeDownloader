using System.Collections.Generic;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace YoutubeApp.Messages;

public class OpenFolderPickerMessage : AsyncRequestMessage<IReadOnlyList<IStorageFolder?>>
{
    public required string Title { get; init; }
    public string? SuggestedStartLocation { get; init; }
}