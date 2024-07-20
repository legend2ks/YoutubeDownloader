using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using YoutubeApp.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class JobDetailsWindowViewModel : ViewModelBase
{
    private readonly IMessenger _messenger;
    public GrabberJob Job { get; }
    public string Title => $"Grabber List {Job.Title}";

    public JobDetailsWindowViewModel(GrabberJob job, IMessenger messenger)
    {
        _messenger = messenger;
        Job = job;
    }

    [RelayCommand]
    private void Retry()
    {
        Job.TryAgain();
    }

    [RelayCommand]
    private void CopyError(string errorMsg)
    {
        _messenger.Send(new SetClipboardTextMessage { Text = errorMsg });
    }
}