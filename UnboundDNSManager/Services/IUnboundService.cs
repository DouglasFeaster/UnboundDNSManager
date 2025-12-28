namespace UnboundDNSManager.Services;

/// <summary>
/// Service interface for executing Unbound commands.
/// </summary>
public interface IUnboundService
{
    /// <summary>
    /// Executes a command on the Unbound server.
    /// </summary>
    /// <param name="command">Command arguments to execute.</param>
    /// <param name="stdinData">Optional stdin data for the command.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A tuple containing success status and command output.</returns>
    Task<(bool success, string output)> ExecuteCommandAsync(
        string[] command,
        string? stdinData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection to the Unbound server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if connection is successful, false otherwise.</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
