using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using OpenGameHUB.Services.Presence;

namespace OpenGameHUB.Infrastructure.Discord;

public sealed class DiscordIpcPresence : IDiscordPresence
{
    private readonly object _gate = new();
    private int _generation;
    private long _retryAfter;
    private NamedPipeClientStream? _pipe;
    private string? _connectedId;

    public void Publish(string? applicationId, DiscordActivity? activity)
    {
        applicationId = applicationId?.Trim();
        if (!DiscordPresenceFrames.IsApplicationId(applicationId) && _connectedId is null)
            return;

        var generation = Interlocked.Increment(ref _generation);
        _ = Task.Run(() => PublishCore(generation, applicationId, activity));
    }

    private void PublishCore(int generation, string? applicationId, DiscordActivity? activity)
    {
        lock (_gate)
        {
            if (generation != Volatile.Read(ref _generation))
                return;

            try
            {
                if (!DiscordPresenceFrames.IsApplicationId(applicationId) || activity is null)
                {
                    Close();
                    return;
                }

                if (!EnsureConnected(applicationId!))
                    return;

                if (generation != Volatile.Read(ref _generation))
                    return;

                var json = DiscordPresenceFrames.SetActivity(
                    Environment.ProcessId,
                    Guid.NewGuid().ToString("N"),
                    activity);
                WriteFrame(DiscordPresenceFrames.FrameOp, json);
                TryReadFrame();
            }
            catch (Exception)
            {
                Close();
                _retryAfter = Environment.TickCount64 + 15_000;
            }
        }
    }

    private bool EnsureConnected(string applicationId)
    {
        if (_pipe is { IsConnected: true } && string.Equals(_connectedId, applicationId, StringComparison.Ordinal))
            return true;

        Close();
        if (Environment.TickCount64 < _retryAfter)
            return false;

        for (var index = 0; index < 4; index++)
        {
            var pipe = new NamedPipeClientStream(
                ".",
                "discord-ipc-" + index,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            try
            {
                pipe.Connect(75);
                _pipe = pipe;
                _connectedId = applicationId;
                WriteFrame(DiscordPresenceFrames.HandshakeOp, DiscordPresenceFrames.Handshake(applicationId));
                if (TryReadFrame())
                    return true;
            }
            catch (Exception)
            {
                pipe.Dispose();
            }

            Close();
        }

        _retryAfter = Environment.TickCount64 + 15_000;
        return false;
    }

    private void WriteFrame(int op, string json)
    {
        var pipe = _pipe ?? throw new IOException("Discord pipe is not connected.");
        var payload = Encoding.UTF8.GetBytes(json);
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, op);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], payload.Length);
        pipe.Write(header);
        pipe.Write(payload);
        pipe.Flush();
    }

    private bool TryReadFrame()
    {
        var pipe = _pipe;
        if (pipe is null)
            return false;

        pipe.ReadTimeout = 1500;
        var header = new byte[8];
        if (ReadExact(pipe, header) != header.Length)
            return false;

        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (length <= 0 || length > 65536)
            return false;

        var payload = new byte[length];
        return ReadExact(pipe, payload) == payload.Length;
    }

    private static int ReadExact(Stream stream, byte[] buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = stream.Read(buffer, read, buffer.Length - read);
            if (count == 0)
                break;

            read += count;
        }

        return read;
    }

    private void Close()
    {
        _pipe?.Dispose();
        _pipe = null;
        _connectedId = null;
    }
}
