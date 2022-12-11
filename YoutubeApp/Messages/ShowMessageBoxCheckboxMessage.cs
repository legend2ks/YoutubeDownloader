using CommunityToolkit.Mvvm.Messaging.Messages;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;

namespace YoutubeApp.Messages;

public class ShowMessageBoxCheckboxMessage : AsyncRequestMessage<CheckboxWindowResultDTO>
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string CheckboxText { get; init; }
    public required ButtonEnum ButtonDefinitions { get; init; }
    public required Icon Icon { get; init; }
}