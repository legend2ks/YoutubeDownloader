using CommunityToolkit.Mvvm.Input;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class JobDetailsWindowViewModel : ViewModelBase
{
    public GrabberJob Job { get; }
    public string Title => $"Grabber List {Job.Title}";

    public JobDetailsWindowViewModel(GrabberJob job)
    {
        Job = job;
    }

    [RelayCommand]
    private void Retry()
    {
        Job.TryAgain();
    }
}