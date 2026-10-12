using System.Runtime.InteropServices;
using Avalonia.Threading;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Infrastructure.Input;

public sealed class XInputGamepadPoller : IDisposable
{
    private const int ErrorSuccess = 0;
    private const ushort DpadUp = 0x0001;
    private const ushort DpadDown = 0x0002;
    private const ushort DpadLeft = 0x0004;
    private const ushort DpadRight = 0x0008;
    private const ushort LeftShoulder = 0x0100;
    private const ushort RightShoulder = 0x0200;
    private const ushort A = 0x1000;
    private const ushort B = 0x2000;
    private const ushort Y = 0x8000;

    private readonly GamepadRepeatState _repeat = new();
    private DispatcherTimer? _timer;
    private GamepadButtons _previous;
    private DateTime _lastPollUtc;
    private bool _hasSample;
    private static bool _unavailable;
    private bool _disposed;
    private static XInputGetStateDelegate? _getState;

    public XInputGamepadPoller()
    {
    }

    public event Action<ConsoleInput>? Input;

    public void Start()
    {
        if (_disposed)
            return;

        _repeat.Reset();
        _previous = default;
        _hasSample = false;
        _lastPollUtc = DateTime.UtcNow;
        _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Input, Dispatcher.UIThread);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    public void Stop() => _timer?.Stop();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_timer is not null)
        {
            _timer.Tick -= OnTick;
            _timer.Stop();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_unavailable || !TryRead(out var current))
        {
            _hasSample = false;
            _repeat.Reset();
            return;
        }

        if (!_hasSample)
        {
            _previous = current;
            _hasSample = true;
            _lastPollUtc = DateTime.UtcNow;
            return;
        }

        var now = DateTime.UtcNow;
        var elapsed = now - _lastPollUtc;
        _lastPollUtc = now;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        foreach (var input in GamepadInput.Collect(_previous, current, _repeat, elapsed))
        {
            try
            {
                Input?.Invoke(input);
            }
            catch (Exception)
            {
                // Keep polling if a handler fails.
            }
        }

        _previous = current;
    }

    private bool TryRead(out GamepadButtons buttons)
    {
        buttons = default;
        if (!EnsureNative())
            return false;

        try
        {
            for (var index = 0; index < 4; index++)
            {
                if (_getState!(index, out var state) != ErrorSuccess)
                    continue;

                var pad = state.Gamepad;
                buttons = new GamepadButtons(
                    A: Has(pad.Buttons, A),
                    B: Has(pad.Buttons, B),
                    Y: Has(pad.Buttons, Y),
                    Left: Has(pad.Buttons, DpadLeft),
                    Right: Has(pad.Buttons, DpadRight),
                    LeftShoulder: Has(pad.Buttons, LeftShoulder),
                    RightShoulder: Has(pad.Buttons, RightShoulder),
                    ThumbX: pad.ThumbLX,
                    Up: Has(pad.Buttons, DpadUp),
                    Down: Has(pad.Buttons, DpadDown),
                    ThumbY: pad.ThumbLY);
                return true;
            }

            return false;
        }
        catch (DllNotFoundException)
        {
            _unavailable = true;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            _unavailable = true;
            return false;
        }
    }

    private static bool EnsureNative()
    {
        if (_getState is not null)
            return true;

        if (_unavailable)
            return false;

        foreach (var libraryName in new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" })
        {
            try
            {
                var library = NativeLibrary.Load(libraryName);
                var export = NativeLibrary.GetExport(library, "XInputGetState");
                _getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateDelegate>(export);
                return true;
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        _unavailable = true;
        return false;
    }

    private static bool Has(ushort buttons, ushort flag) => (buttons & flag) != 0;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int XInputGetStateDelegate(int userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }
}
