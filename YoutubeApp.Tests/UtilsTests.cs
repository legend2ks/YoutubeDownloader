namespace YoutubeApp.Tests;

public class UtilsTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1023 B", 1023)]
    [InlineData("1 KB", 1024)]
    [InlineData("1 MB", 1024 * 1024)]
    [InlineData("1 GB", 1024 * 1024 * 1024)]
    public void FormatBytesShouldReturnTheCorrectFormattedSize(string expected, long bytes)
    {
        //Act
        var formattedSize = Utils.FormatBytes(bytes);

        //Assert
        Assert.Equal(expected, formattedSize);
    }

    [Theory]
    [InlineData("0s", 0)]
    [InlineData("59s", 59)]
    [InlineData("1:00", 60)]
    [InlineData("59:00", 60 * 59)]
    [InlineData("1:00:00", 60 * 60)]
    [InlineData("23:00:00", 60 * 60 * 23)]
    public void DurationStringFromSecondsShouldReturnTheCorrectDurationString(string expected, int seconds)
    {
        //Act
        var durationString = Utils.DurationStringFromSeconds(seconds);

        //Assert
        Assert.Equal(expected, durationString);
    }

    [Theory]
    [InlineData(@"C:", @"C:\AB", @"C:\ABC")]
    [InlineData(@"C:\AB", @"C:\AB\CD", @"C:\AB\EF")]
    [InlineData(@"C:\AB\CD", @"C:\AB\CD", @"C:\AB\CD\EF")]
    [InlineData(@"C:\AB", @"C:\AB", @"C:\ab")]
    [InlineData(@"", @"C:\AB", @"D:\AB")]
    public void FindCommonPathShouldReturnTheCommonPath(string expected, params string[] paths)
    {
        //Act
        var commonPath = Utils.FindCommonPath(paths);

        //Assert
        Assert.Equal(expected, commonPath);
    }
}