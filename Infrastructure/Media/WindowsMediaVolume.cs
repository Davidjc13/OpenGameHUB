using System.Runtime.InteropServices;
using OpenGameHUB.Services.Media;

namespace OpenGameHUB.Infrastructure.Media;

public sealed class WindowsMediaVolume : IMediaVolumeController
{
    public bool TryGet(string processName, out MediaVolumeLevel level)
    {
        level = default;
        if (!CanTarget(processName))
            return false;

        try
        {
            MediaVolumeLevel? found = null;
            ForEachSession(processName, volume =>
            {
                if (found is not null)
                    return;

                volume.GetMasterVolume(out var master);
                volume.GetMute(out var muted);
                found = new MediaVolumeLevel(master, muted);
            });

            if (found is not { } value)
                return false;

            level = value;
            return true;
        }
        catch (Exception ex) when (IsAudioFailure(ex))
        {
            return false;
        }
    }

    public bool TrySet(string processName, float level, bool muted)
    {
        if (!CanTarget(processName))
            return false;

        var clamped = Math.Clamp(level, 0f, 1f);
        try
        {
            var changed = false;
            ForEachSession(processName, volume =>
            {
                var context = Guid.Empty;
                if (volume.SetMasterVolume(clamped, ref context) == 0
                    && volume.SetMute(muted, ref context) == 0)
                {
                    changed = true;
                }
            });
            return changed;
        }
        catch (Exception ex) when (IsAudioFailure(ex))
        {
            return false;
        }
    }

    private static bool CanTarget(string processName) =>
        !string.IsNullOrWhiteSpace(processName) && !MediaVolumeTarget.IsDiscordVoice(processName);

    private static void ForEachSession(string processName, Action<ISimpleAudioVolume> visit)
    {
        var enumeratorType = new MMDeviceEnumeratorComObject();
        var enumerator = (IMMDeviceEnumerator)enumeratorType;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device is null)
                return;

            var managerId = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref managerId, 23, IntPtr.Zero, out var activated) != 0
                || activated is not IAudioSessionManager2 sessionManager)
            {
                return;
            }

            manager = sessionManager;
            if (manager.GetSessionEnumerator(out sessions) != 0 || sessions is null)
                return;

            if (sessions.GetCount(out var count) != 0 || count <= 0)
                return;

            for (var i = 0; i < count; i++)
            {
                IAudioSessionControl2? session = null;
                ISimpleAudioVolume? volume = null;
                try
                {
                    if (sessions.GetSession(i, out session) != 0 || session is null)
                        continue;

                    if (session.GetProcessId(out var pid) != 0 || pid == 0 || !ProcessMatches(pid, processName))
                        continue;

                    volume = QueryVolume(session);
                    if (volume is null)
                        continue;

                    visit(volume);
                }
                finally
                {
                    if (volume is not null)
                        Marshal.ReleaseComObject(volume);
                    if (session is not null)
                        Marshal.ReleaseComObject(session);
                }
            }
        }
        finally
        {
            if (sessions is not null)
                Marshal.ReleaseComObject(sessions);
            if (manager is not null)
                Marshal.ReleaseComObject(manager);
            if (device is not null)
                Marshal.ReleaseComObject(device);
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static bool ProcessMatches(uint pid, string processName)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            return process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static ISimpleAudioVolume? QueryVolume(IAudioSessionControl2 session)
    {
        var unknown = IntPtr.Zero;
        var volumePointer = IntPtr.Zero;
        try
        {
            unknown = Marshal.GetIUnknownForObject(session);
            var iid = typeof(ISimpleAudioVolume).GUID;
            if (Marshal.QueryInterface(unknown, ref iid, out volumePointer) != 0 || volumePointer == IntPtr.Zero)
                return null;

            return (ISimpleAudioVolume)Marshal.GetObjectForIUnknown(volumePointer);
        }
        finally
        {
            if (volumePointer != IntPtr.Zero)
                Marshal.Release(volumePointer);
            if (unknown != IntPtr.Zero)
                Marshal.Release(unknown);
        }
    }

    private static bool IsAudioFailure(Exception exception) =>
        exception is COMException or InvalidCastException or InvalidComObjectException or EntryPointNotFoundException;

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int OpenPropertyStore(int access, out IntPtr store);

        [PreserveSig]
        int GetId(out IntPtr id);

        [PreserveSig]
        int GetState(out int state);

        [PreserveSig]
        int Activate(
            ref Guid iid,
            uint classContext,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig]
        int GetAudioSessionControl(ref Guid sessionGuid, uint flags, out IntPtr session);

        [PreserveSig]
        int GetSimpleAudioVolume(ref Guid sessionGuid, uint flags, out IntPtr volume);

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig]
        int GetState(out int state);

        [PreserveSig]
        int GetDisplayName(out IntPtr name);

        [PreserveSig]
        int SetDisplayName(IntPtr name, ref Guid context);

        [PreserveSig]
        int GetIconPath(out IntPtr path);

        [PreserveSig]
        int SetIconPath(IntPtr path, ref Guid context);

        [PreserveSig]
        int GetGroupingParam(out Guid grouping);

        [PreserveSig]
        int SetGroupingParam(ref Guid grouping, ref Guid context);

        [PreserveSig]
        int RegisterAudioSessionNotification(IntPtr notification);

        [PreserveSig]
        int UnregisterAudioSessionNotification(IntPtr notification);

        [PreserveSig]
        int GetSessionIdentifier(out IntPtr identifier);

        [PreserveSig]
        int GetSessionInstanceIdentifier(out IntPtr identifier);

        [PreserveSig]
        int GetProcessId(out uint processId);

        [PreserveSig]
        int IsSystemSoundsSession();

        [PreserveSig]
        int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolume(out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
