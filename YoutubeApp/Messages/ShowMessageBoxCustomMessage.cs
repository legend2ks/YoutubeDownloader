using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;

namespace YoutubeApp.Messages;

public class ShowMessageBoxCustomMessage : AsyncRequestMessage<string>
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required IEnumerable<ButtonDefinition> ButtonDefinitions { get; init; }
    public required Icon Icon { get; init; }
}