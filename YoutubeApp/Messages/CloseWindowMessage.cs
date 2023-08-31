using CommunityToolkit.Mvvm.Messaging.Messages;

namespace YoutubeApp.Messages;

public class CloseWindowMessage<T> : ValueChangedMessage<T>
{
    public CloseWindowMessage(T value) : base(value)
    {
    }
}