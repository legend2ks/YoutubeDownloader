using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace YoutubeApp;

public partial class MoveOp
{
    private enum CopyProgressCallbackReason : uint
    {
        CALLBACK_CHUNK_FINISHED = 0x00000000,
        CALLBACK_STREAM_SWITCH = 0x00000001
    }

    private enum CopyProgressResult : uint
    {
        PROGRESS_CONTINUE = 0,
        PROGRESS_CANCEL = 1,
        PROGRESS_STOP = 2,
        PROGRESS_QUIET = 3
    }

    private readonly CancellationToken _cancellationToken;

    public MoveOp(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public async Task MoveFileAsync(string sourceFileName, string destFileName, bool overwrite)
    {
        var flags = MoveFileFlags.MOVEFILE_COPY_ALLOWED;
        if (overwrite)
        {
            flags |= MoveFileFlags.MOVEFILE_REPLACE_EXISTING;
        }

        var lastError =
            await Task.Run(
                () =>
                {
                    var success = MoveFileWithProgressW(sourceFileName, destFileName, CopyProgressHandler, IntPtr.Zero,
                        flags);
                    ;
                    return success ? 0 : Marshal.GetLastWin32Error();
                },
                _cancellationToken);
        _cancellationToken.ThrowIfCancellationRequested();
        if (lastError != 0)
        {
            throw new Win32Exception(lastError);
        }
    }

    private CopyProgressResult CopyProgressHandler(long totalFileSize, long totalBytesTransferred, long streamSize,
        long streamBytesTransferred, uint dwStreamNumber, CopyProgressCallbackReason dwCallbackReason,
        IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return CopyProgressResult.PROGRESS_CANCEL;
        }

        return CopyProgressResult.PROGRESS_CONTINUE;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "MoveFileWithProgressW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MoveFileWithProgressW(
        string lpExistingFileName,
        string lpNewFileName,
        CopyProgressRoutine lpProgressRoutine,
        IntPtr lpData,
        MoveFileFlags dwFlags);

    [Flags]
    private enum MoveFileFlags : uint
    {
        MOVEFILE_REPLACE_EXISTING = 0x00000001,
        MOVEFILE_COPY_ALLOWED = 0x00000002,
        MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004,
        MOVEFILE_WRITE_THROUGH = 0x00000008,
        MOVEFILE_CREATE_HARDLINK = 0x00000010,
        MOVEFILE_FAIL_IF_NOT_TRACKABLE = 0x00000020,
    }

    private delegate CopyProgressResult CopyProgressRoutine(
        long totalFileSize,
        long totalBytesTransferred,
        long streamSize,
        long streamBytesTransferred,
        uint dwStreamNumber,
        CopyProgressCallbackReason dwCallbackReason,
        IntPtr hSourceFile,
        IntPtr hDestinationFile,
        IntPtr lpData);
}