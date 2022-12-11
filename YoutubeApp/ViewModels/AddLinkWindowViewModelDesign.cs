namespace YoutubeApp.ViewModels;

internal class AddLinkWindowViewModelDesign : AddLinkWindowViewModel
{
    public AddLinkWindowViewModelDesign() : base(null!, null!)
    {
        CurrentPage = 0;
        VideosWithPlaylist = new()
        {
            new()
            {
                Link = "Link1", VideoIds = new() { "AAAAAAAAAAA", "BBBBBBBBBBB", "CCCCCCCCCC" },
                PlaylistId = "ffffffffffffffffffffffffffffffffff"
            },
            new()
            {
                Link = "Link2", VideoIds = new() { "DDDDDDDDDDD", "EEEEEEEEEEE" },
                PlaylistId = "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh"
            },
        };
    }
}