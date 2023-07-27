using System.Collections.Generic;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

internal class MainWindowViewModelDesign : MainWindowViewModel
{
    public MainWindowViewModelDesign()
    {
        DownloaderReady = true;
        DownloadSpeed = 1258291;
        WindowWidth = 900;
        WindowHeight = 400;

        Grabber = new Grabber(null!, null!)
        {
            GrabberJobs =
            {
                new GrabberJob(
                    1,
                    new List<string>(),
                    new List<string>(),
                    @"C:\Videos",
                    "#1",
                    null,
                    null!
                )
                {
                    IsFinished = true,
                },
                new GrabberJob(
                    2,
                    new List<string>(),
                    new List<string>(),
                    @"D:\Videos",
                    "#2 | Uploads from dotnet",
                    null,
                    null!
                )
                {
                    DoneCount = 274,
                    ErrorCount = 149,
                    TotalCount = 395,
                },
                new GrabberJob(
                    3,
                    new List<string>(),
                    new List<string>(),
                    @"E:\Videos",
                    "#3 | Uploads from Avalonia UI",
                    null,
                    null!
                )
                {
                    IsFailed = true,
                },
            }
        };
    }
}