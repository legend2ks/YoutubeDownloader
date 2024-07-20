using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

internal class JobDetailsWindowViewModelDesign : JobDetailsWindowViewModel
{
    public JobDetailsWindowViewModelDesign() : base(
        new GrabberJob(1, new(), new(), "", "Uploads from Learn.NET", null, null!)
        {
            Playlists = new()
            {
                {
                    "AAABBBCCC", new GrabberJobPlaylist()
                    {
                        Title = "Test Playlist One", Videos = new()
                        {
                            new()
                            {
                                VideoId = "AAAAAAAAAAA", Title = "Test Video 1", Status = VideoStatus.Done,
                                StatusText = "Added ✔"
                            },
                            new()
                            {
                                VideoId = "BBBBBBBBBBB", Title = "Test Video 2", Status = VideoStatus.Duplicate,
                                StatusText = "Duplicate ✖"
                            },
                        }
                    }
                },
                {
                    "DDDEEEFFF", new GrabberJobPlaylist()
                    {
                        Title = "Test Playlist Two", Videos = new()
                        {
                            new()
                            {
                                VideoId = "CCCCCCCCCCC", Title = "Test Video 1", Status = VideoStatus.Error,
                                StatusText = "Error ❌",
                                ErrorMessage = "This is the error message from YouTube.\nLine two."
                            },
                            new()
                            {
                                VideoId = "DDDDDDDDDDD", Title = "Test Video 2", Status = VideoStatus.Fetching,
                                StatusText = "Fetching  ◻ "
                            },
                            new()
                            {
                                VideoId = "EEEEEEEEEEE", Title = "Test Video 3", Status = VideoStatus.Waiting,
                                StatusText = "Waiting  ◽ "
                            },
                        }
                    }
                },
            },
            ErrorCount = 7,
            DuplicateCount = 3,
            IsFinished = true,
        }, null!)
    {
    }
}