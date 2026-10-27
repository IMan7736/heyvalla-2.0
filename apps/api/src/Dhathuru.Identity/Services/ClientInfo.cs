using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace Dhathuru.Identity.Services;

/// <summary>What we remember about the calling device. Deliberately coarse: enough to recognise a session, not to track a person.</summary>
public sealed record ClientInfo(string? NetworkHint, string? UserAgent)
{
    public static ClientInfo From(HttpContext? context)
    {
        if (context is null) return new ClientInfo(null, null);
        var agent = context.Request.Headers.UserAgent.ToString();
        if (agent.Length > 200) agent = agent[..200];
        return new ClientInfo(Truncate(context.Connection.RemoteIpAddress), string.IsNullOrWhiteSpace(agent) ? null : agent);
    }

    /// <summary>IPv4 keeps /24 (a.b.c.0), IPv6 keeps /48.</summary>
    public static string? Truncate(IPAddress? address)
    {
        if (address is null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        switch (address.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0/24";
            case AddressFamily.InterNetworkV6:
                for (var i = 6; i < bytes.Length; i++) bytes[i] = 0;
                return new IPAddress(bytes) + "/48";
            default:
                return null;
        }
    }
}
