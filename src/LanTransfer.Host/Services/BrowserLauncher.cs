using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LanTransfer.Services;

public static class BrowserLauncher
{
    public static bool TryOpen(string url, ILogger logger)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to open the default browser at {Url}.", url);
            return false;
        }
    }
}
