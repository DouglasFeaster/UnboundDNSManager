using Microsoft.Extensions.Options;
using System.Net.Sockets;
using UnboundDNSManager.Models;

namespace UnboundDNSManager.Services;

public class UnboundService : IUnboundService
{
    private readonly UnboundConnectionOptions _connectionOptions;
    private readonly ILogger<UnboundService> _logger;
    private readonly ILogger<UnboundControlClient> _clientLogger;

    public UnboundService(
            IOptions<UnboundConnectionOptions> connectionOptions,
            ILogger<UnboundService> logger,
            ILogger<UnboundControlClient> clientLogger)
    {
        ArgumentNullException.ThrowIfNull(connectionOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionOptions = connectionOptions.Value;
        _logger = logger;
        _clientLogger = clientLogger;
    }

    public async Task<(bool success, string output)> ExecuteCommandAsync(
           string[] command,
           string? stdinData = null,
           CancellationToken cancellationToken = default)
    {
        using var client = new UnboundControlClient(_connectionOptions, _clientLogger);

        try
        {
            await client.ConnectAsync();
            var result = await client.SendCommandAsync(command, cancellationToken, stdinData);

            _logger.LogInformation("Command execution {Status}: {Command}",
                    result.success ? "succeeded" : "failed",
                    string.Join(" ", command));

            return result;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            _logger.LogError(ex, "Connection refused to Unbound server at {Host}:{Port}",
                    _connectionOptions.Host, _connectionOptions.Port);
            return (false, "error: unbound server is not running or not accessible");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout connecting to Unbound server");
            return (false, $"error: connection timeout - {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Unbound server for command: {Command}",
                string.Join(" ", command));
            return (false, $"error: {ex.Message}");
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (success, output) = await ExecuteCommandAsync(
                ["status"],
                null);

            if (success)
            {
                _logger.LogInformation("Connection test successful");
                return true;
            }
            else
            {
                _logger.LogWarning("Connection test failed: {Output}", output);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed with exception");
            return false;
        }
    }
}
