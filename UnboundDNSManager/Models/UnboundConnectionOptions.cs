namespace UnboundDNSManager.Models;

/// <summary>
/// Configuration options for connecting to an Unbound server.
/// </summary>
public sealed record UnboundConnectionOptions
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public bool UseSSL { get; init; }
    public string? ServerCertFile { get; init; }
    public string? ControlCertFile { get; init; }
    public string? ControlKeyFile { get; init; }
    public int ConnectTimeoutMs { get; init; } = 5000;
}