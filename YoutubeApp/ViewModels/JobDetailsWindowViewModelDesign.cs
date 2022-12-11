using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

internal class JobDetailsWindowViewModelDesign : JobDetailsWindowViewModel
{
    public JobDetailsWindowViewModelDesign() : base(new GrabberJob(1, new(), new(), "", "Uploads from Learn.NET", null!)
    {
        Playlists = new()
        {
            {
                "AAABBBCCC", new GrabberJobPlaylist()
                {
                    Title = "Test Playlist One", Videos = new()
                    {
                        new() { VideoId = "AAAAAAAAAAA", Title = "Test Video 1", Status = VideoStatus.Done },
                        new() { VideoId = "BBBBBBBBBBB", Title = "Test Video 2", Status = VideoStatus.Done },
                    }
                }
            },
            {
                "DDDEEEFFF", new GrabberJobPlaylist()
                {
                    Title = "Test Playlist Two", Videos = new()
                    {
                        new() { VideoId = "CCCCCCCCCCC", Title = "Test Video 1", Status = VideoStatus.Done },
                        new() { VideoId = "DDDDDDDDDDD", Title = "Test Video 2", Status = VideoStatus.Error },
                        new() { VideoId = "EEEEEEEEEEE", Title = "Test Video 3", Status = VideoStatus.Waiting },
                        new() { VideoId = "FFFFFFFFFFF", Title = "Test Video 4", Status = VideoStatus.Waiting },
                    }
                }
            },
        },
        ErrorCount = 7,
        DuplicateCount = 3,
        IsFinished = true,
    })
    {
    }
}