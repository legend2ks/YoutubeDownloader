using CommunityToolkit.Mvvm.Messaging.Messages;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace YoutubeApp.Messages;

public class ShowMessageBoxCheckboxMessage : AsyncRequestMessage<MessageBoxCheckboxResult>
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string CheckboxText { get; init; }
    public bool CheckboxDefaultState { get; set; }
    public required ButtonEnum ButtonDefinitions { get; init; }
    public required Icon Icon { get; init; }
}