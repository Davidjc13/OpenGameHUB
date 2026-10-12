using System.Runtime.InteropServices;
using OpenGameHUB.Services.Media;

namespace OpenGameHUB.Infrastructure.Media;

internal static class MediaKeySender
{
    private const byte NextTrack = 0xB0;
    private const byte PreviousTrack = 0xB1;
    private const byte PlayPause = 0xB3;
    private const uint KeyUp = 0x0002;

    public static void Tap(MediaTransport transport)
    {
        var key = transport switch
        {
            MediaTransport.Next => NextTrack,
            MediaTransport.Previous => PreviousTrack,
            _ => PlayPause
        };
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyUp, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
