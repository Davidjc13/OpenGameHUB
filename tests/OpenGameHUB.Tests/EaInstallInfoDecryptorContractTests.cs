using System.Reflection;
using GameFinder.Common;
using GameFinder.StoreHandlers.EADesktop;
using GameFinder.StoreHandlers.EADesktop.Crypto;
using NexusMods.Paths;
using OneOf;

namespace OpenGameHUB.Tests;

/// <summary>
/// Guards the reflection bridge in <see cref="OpenGameHUB.Providers.Ea.EaInstallInfoDecryptor"/>.
/// Fails at build/test time when GameFinder renames or changes the private API we depend on.
/// </summary>
public sealed class EaInstallInfoDecryptorContractTests
{
    private const string DecryptMethodName = "DecryptInstallInfoFile";

    [Fact]
    public void GameFinder_EADesktopHandler_DecryptInstallInfoFile_matches_expected_contract()
    {
        var method = typeof(EADesktopHandler).GetMethod(
            DecryptMethodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method!.IsStatic, $"{DecryptMethodName} must remain static.");

        AssertContract(
            method,
            expectedReturnType: typeof(OneOf<string, ErrorMessage>),
            expectedParameterTypes:
            [
                typeof(IFileSystem),
                typeof(AbsolutePath),
                typeof(IHardwareInfoProvider)
            ]);
    }

    [Fact]
    public void GameFinder_IHardwareInfoProvider_exposes_expected_members()
    {
        var expectedMembers = new[]
        {
            nameof(IHardwareInfoProvider.GetVolumeSerialNumber),
            nameof(IHardwareInfoProvider.GetBaseBoardManufacturer),
            nameof(IHardwareInfoProvider.GetBaseBoardSerialNumber),
            nameof(IHardwareInfoProvider.GetBIOSManufacturer),
            nameof(IHardwareInfoProvider.GetBIOSSerialNumber),
            nameof(IHardwareInfoProvider.GetVideoControllerDeviceId),
            nameof(IHardwareInfoProvider.GetProcessorManufacturer),
            nameof(IHardwareInfoProvider.GetProcessorId),
            nameof(IHardwareInfoProvider.GetProcessorName),
        };

        foreach (var member in expectedMembers)
        {
            var method = typeof(IHardwareInfoProvider).GetMethod(member);
            Assert.NotNull(method);
            Assert.Equal(typeof(string), method!.ReturnType);
        }
    }

    private static void AssertContract(
        MethodInfo method,
        Type expectedReturnType,
        IReadOnlyList<Type> expectedParameterTypes)
    {
        Assert.Equal(expectedReturnType, method.ReturnType);

        var parameters = method.GetParameters();
        Assert.True(
            parameters.Length == expectedParameterTypes.Count,
            $"Expected {expectedParameterTypes.Count} parameters, found {parameters.Length} " +
            $"for {method.DeclaringType!.FullName}.{method.Name}.");

        for (var i = 0; i < expectedParameterTypes.Count; i++)
        {
            Assert.True(
                expectedParameterTypes[i] == parameters[i].ParameterType,
                $"Parameter {i} type mismatch for {method.DeclaringType!.FullName}.{method.Name}: " +
                $"expected {expectedParameterTypes[i].FullName}, got {parameters[i].ParameterType.FullName}.");
        }
    }
}
