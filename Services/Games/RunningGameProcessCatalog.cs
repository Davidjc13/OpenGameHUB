using System.Diagnostics;
using OpenGameHUB.Infrastructure.Windows;

namespace OpenGameHUB.Services.Games;

internal sealed class RunningGameProcessCatalog : IGameProcessCatalog
{
    public IReadOnlyList<GameProcessInfo> Snapshot()
    {
        var result = new List<GameProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                result.Add(new GameProcessInfo(
                    process.Id,
                    ProcessImagePath.TryGet(process),
                    process.ProcessName));
            }
            catch (Exception)
            {
                // Skip processes we cannot inspect.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }
}
