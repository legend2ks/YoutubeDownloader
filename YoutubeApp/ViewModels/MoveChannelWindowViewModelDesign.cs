namespace YoutubeApp.ViewModels;

public class MoveChannelWindowViewModelDesign : MoveChannelWindowViewModel
{
    public MoveChannelWindowViewModelDesign()
    {
        SourcePath = @"C:\Users\Username\Videos\ChannelName";
        DestPath = @"D:\Youtube\ChannelName";
        TotalCount = 4123;
        DoneCount = 756;
        FileItems.Add(new FileItem
            { Filename = "[2000.01.01][aaaaaaaa][ChannelName] Video Title 1.mp4", Status = "Moving..." });
        FileItems.Add(new FileItem
            { Filename = "[2000.01.01][bbbbbbbb][ChannelName] Video Title 2.mp4", Status = "Moved" });
        FileItems.Add(new FileItem
        {
            Filename = "[2000.01.01][cccccccc][ChannelName] Video Title 3.mp4", Status = "Skipped",
            Details = "Destination file already exists."
        });
        FileItems.Add(new FileItem
            { Filename = "[2000.01.01][dddddddd][ChannelName] Video Title 4.mp4", Status = "Cancelled" });
    }
}