cd ..
$publishFolder = "Publish"
$publishFolderApp = "$publishFolder/app"

# Cleanup:
Write-Host -F Blue "Cleaning up folder: '$publishFolderApp'"
if (test-path $publishFolderApp)
{
    Remove-Item "$publishFolderApp/*" -Force -Recurse
    Write-Host -F Yellow "Removed folder: $publishFolderApp"
}

# Build as .exe:
# https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
Write-Host -F Blue "Publishing app..."
dotnet publish "YoutubeApp/YoutubeApp.csproj" -c "Release" -o $publishFolderApp -p:DebugType=None -p:PublishSingleFile=true --self-contained false # -r "win-x64"
if ($LASTEXITCODE -ne 0)
{
    Write-Host -F Red "Publishing app FAILED!"
    exit -1
}
Write-Host -F Green "Publishing app DONE!"

mkdir "$publishFolderApp/utils" -Force

# TODO: download:
# - "aria2c.exe" from https://github.com/aria2/aria2/releases (look for the releases file download `aria2-1.37.0-win-64bit-build1.zip`).
# - "yt-dlp.exe" from https://github.com/yt-dlp/yt-dlp/releases (look for the `yt-dlp.exe` file)
# - "ffmpeg.exe" from https://github.com/BtbN/FFmpeg-Builds/releases (for example the `ffmpeg-master-latest-win64-lgpl.zip` and extract the `ffmpeg.exe`)

# TODO: compress:
# $version = (Get-Item "$publishFolderApp/YoutubeApp.exe").VersionInfo.FileVersion
# $destinationZip = "$publishFolder/YoutubeApp-v$version.zip"
# Write-Host -F Blue "Compressing binaries into: '$destinationZip'..."
# https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.archive/compress-archive?view=powershell-7.3
# Compress-Archive -Path "$publishFolderApp/*" -DestinationPath $destinationZip -Force

Start $publishFolder

Write-Host -F Green "Finished Build!"
