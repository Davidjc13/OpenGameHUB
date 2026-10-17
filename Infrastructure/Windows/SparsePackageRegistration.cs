using System.Runtime.InteropServices;
using Windows.Management.Deployment;

namespace OpenGameHUB.Infrastructure.Windows;

internal static class SparsePackageRegistration
{
    public const string ManifestFileName = "Package.appxmanifest";
    private const int AppModelErrorNoPackage = 15700;
    private static int _attempted;

    public static bool HasPackageIdentity()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            var length = 0u;
            var result = GetCurrentPackageFullName(ref length, IntPtr.Zero);
            return result != AppModelErrorNoPackage;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void TryEnsureRegistered()
    {
        if (Interlocked.Exchange(ref _attempted, 1) != 0)
            return;

        if (!OperatingSystem.IsWindows() || HasPackageIdentity())
            return;

        var baseDir = AppContext.BaseDirectory;
        var manifestPath = Path.Combine(baseDir, ManifestFileName);
        if (!File.Exists(manifestPath))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var manager = new PackageManager();
                var manifestUri = new Uri(manifestPath, UriKind.Absolute);
                var externalDir = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var externalUri = new Uri(externalDir, UriKind.Absolute);
                var options = new RegisterPackageOptions
                {
                    DeveloperMode = true,
                    ExternalLocationUri = externalUri
                };

                await manager.RegisterPackageByUriAsync(manifestUri, options).AsTask().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(SparsePackageRegistration),
                    operation: nameof(TryEnsureRegistered),
                    exception: ex,
                    details: manifestPath);
            }
        });
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
