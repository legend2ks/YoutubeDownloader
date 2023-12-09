using System.Collections.ObjectModel;
using System.Linq;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public class ChannelsViewModelDesign : ChannelsViewModel
{
    public ChannelsViewModelDesign()
    {
        CurrentPage = 0;
        ChannelCategories = new ObservableCollection<ChannelCategory>
        {
            new()
            {
                Id = 1,
                Title = "Category One",
                Parent = 0,
                Channels = new ObservableCollection<Channel>(Enumerable.Range(0, 7).Select(x => new Channel
                {
                    Id = x,
                    ListId = "eprge3pm4dwpg349jgegwdzpoin",
                    Title = $"Channel Test Derived {x}",
                    Path = @$"C:\Test{x}",
                    LastUpdate = "1/5/2023",
                    CategoryId = x % 5 + 1,
                    AddedVideoCount = x % 3 * (x + 1) * 7,
                }))
            },
            new()
            {
                Id = 2,
                Title = "Category Two",
                Parent = 0,
            },
            new()
            {
                Id = 3,
                Title = "Category Three",
                Parent = 2,
            },
            new()
            {
                Id = 4,
                Title = "Category Four",
                Parent = 2,
            },
            new()
            {
                Id = 5,
                Title = "Category Five",
                Parent = 0,
            },
        };
        SelectedChannel = new Channel { Title = "Channel Title" };
        Videos = Enumerable.Range(0, 30).Select(x => new Video
        {
            Id = x,
            VideoId = $"videoid#{x}",
            Title = string.Join(" ", new string[x % 4 * 3 + 1].Select(_ => "Abcde")),
            ChannelId = 1,
            Watched = x % 3 == 0,
            Duration = $"{x}:00",
            FileName = x % 4 == 0 ? @"F:\ile\Name" : null,
        }).ToList();
    }
}