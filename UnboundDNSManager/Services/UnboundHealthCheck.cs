using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace UnboundDNSManager.Services;

/// <summary>
/// Health check for Unbound server connectivity.
/// </summary>
public sealed class UnboundHealthCheck : IHealthCheck
{
    private readonly IUnboundService _unboundService;
    private readonly ILogger<UnboundHealthCheck> _logger;

    public UnboundHealthCheck(
        IUnboundService unboundService,
        ILogger<UnboundHealthCheck> logger)
    {
        _unboundService = unboundService ?? throw new ArgumentNullException(nameof(unboundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _unboundService.TestConnectionAsync(cancellationToken);

            if (isHealthy)
            {
                return HealthCheckResult.Healthy("Unbound server is responding");
            }
            else
            {
                return HealthCheckResult.Unhealthy("Unbound server is not responding");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Health check cancelled");
            return HealthCheckResult.Unhealthy("Health check was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            return HealthCheckResult.Unhealthy(
                "Failed to connect to Unbound server",
                exception: ex);
        }
    }
}
