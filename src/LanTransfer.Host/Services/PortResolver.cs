using System.Net;
using System.Net.Sockets;

namespace LanTransfer.Services;

/// <summary>
/// Resolves a usable local listening port without requiring administrator rights.
/// Windows can reserve port ranges (for example, Hyper-V's excluded ranges), and
/// those ports fail with AccessDenied even when no process is listening on them.
/// </summary>
public static class PortResolver
{
    public static int Resolve(int requestedPort)
    {
        if (requestedPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("LanTransfer:Port must be between 1 and 65535.");
        }

        return CanBind(requestedPort) ? requestedPort : GetSystemAssignedPort();
    }

    private static bool CanBind(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            return true;
        }
        catch (SocketException ex) when (
            ex.SocketErrorCode is SocketError.AccessDenied or
            SocketError.AddressAlreadyInUse or
            SocketError.AddressNotAvailable)
        {
            return false;
        }
    }

    private static int GetSystemAssignedPort()
    {
        using var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
