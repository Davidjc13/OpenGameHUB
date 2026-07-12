using System.Management;
using System.Reflection;
using GameFinder.Common;
using GameFinder.StoreHandlers.EADesktop;
using GameFinder.StoreHandlers.EADesktop.Crypto;
using GameFinder.StoreHandlers.EADesktop.Crypto.Windows;
using NexusMods.Paths;
using OneOf;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Ea;

internal static class EaInstallInfoDecryptor
{
    private static readonly Lazy<MethodInfo?> DecryptMethod = new(() =>
        typeof(EADesktopHandler).GetMethod(
            "DecryptInstallInfoFile",
            BindingFlags.NonPublic | BindingFlags.Static));

    public static string? TryDecrypt(string installInfoPath)
    {
        if (string.IsNullOrWhiteSpace(installInfoPath) || !File.Exists(installInfoPath))
            return null;

        var method = DecryptMethod.Value;
        if (method is null)
            return null;

        var installInfoFile = FileSystem.Shared.FromUnsanitizedFullPath(installInfoPath);
        var baseProvider = new HardwareInfoProvider();
        var providers = BuildHardwareCandidates(baseProvider);

        foreach (var provider in providers)
        {
            try
            {
                var result = (OneOf<string, ErrorMessage>)method.Invoke(
                    null,
                    [FileSystem.Shared, installInfoFile, provider])!;

                if (!result.TryPickT0(out var plaintext, out _))
                    continue;

                if (IsValidInstallInfoJson(plaintext))
                    return plaintext;
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(EaInstallInfoDecryptor),
                    operation: "TryDecrypt",
                    exception: ex,
                    platform: Platform.Ea,
                    details: installInfoPath);
            }
        }

        return null;
    }

    private static List<IHardwareInfoProvider> BuildHardwareCandidates(IHardwareInfoProvider baseProvider)
    {
        var providers = new List<IHardwareInfoProvider> { baseProvider };
        var defaultGpu = SafeGet(() => baseProvider.GetVideoControllerDeviceId());

        foreach (var gpuId in GetVideoControllerDeviceIds())
        {
            if (string.Equals(gpuId, defaultGpu, StringComparison.OrdinalIgnoreCase))
                continue;

            providers.Add(new OverrideVideoControllerHardwareInfoProvider(baseProvider, gpuId));
        }

        return providers;
    }

    private static List<string> GetVideoControllerDeviceIds()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT PNPDeviceId FROM Win32_VideoController");
            using var results = searcher.Get();
            return results
                .Cast<ManagementBaseObject>()
                .Select(o => o["PNPDeviceId"] as string)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(EaInstallInfoDecryptor),
                operation: "GetVideoControllerDeviceIds",
                exception: ex,
                platform: Platform.Ea);
            return [];
        }
    }

    private static bool IsValidInstallInfoJson(string plaintext) =>
        plaintext.Contains("installInfos", StringComparison.Ordinal);

    private static string? SafeGet(Func<string> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private sealed class OverrideVideoControllerHardwareInfoProvider(
        IHardwareInfoProvider inner,
        string videoControllerDeviceId) : IHardwareInfoProvider
    {
        public string GetVolumeSerialNumber() => inner.GetVolumeSerialNumber();
        public string GetBaseBoardManufacturer() => inner.GetBaseBoardManufacturer();
        public string GetBaseBoardSerialNumber() => inner.GetBaseBoardSerialNumber();
        public string GetBIOSManufacturer() => inner.GetBIOSManufacturer();
        public string GetBIOSSerialNumber() => inner.GetBIOSSerialNumber();
        public string GetVideoControllerDeviceId() => videoControllerDeviceId;
        public string GetProcessorManufacturer() => inner.GetProcessorManufacturer();
        public string GetProcessorId() => inner.GetProcessorId();
        public string GetProcessorName() => inner.GetProcessorName();
    }
}
