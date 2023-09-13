using System.Runtime.InteropServices;

namespace YoutubeApp;

public static partial class Keyboard
{
    [LibraryImport("user32.dll")]
    private static partial ushort GetAsyncKeyState(Key key);

    public static bool IsKeyDown(Key key)
    {
        return GetAsyncKeyState(key) >= 0x8000;
    }

    public enum Key
    {
        SHIFT = 0x10,
        CONTROL = 0x11,
        ALT = 0x12,
    }
}