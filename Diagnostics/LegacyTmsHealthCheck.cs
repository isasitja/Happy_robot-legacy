using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TMS_API.Services;

namespace TMS_API.Diagnostics
{
    /// <summary>
    /// Readiness probe for the Legacy TMS connection. Uses DEBUG_ECHO, which bypasses the
    /// server's fault injection, so a failure indicates a real connectivity or auth problem.
    /// </summary>
    public class LegacyTmsHealthCheck : IHealthCheck
    {
        private const string Probe = "healthcheck";

        private readonly ILegacyTmsClient _client;

        public LegacyTmsHealthCheck(ILegacyTmsClient client)
        {
            _client = client;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await _client.EchoAsync(Probe, cancellationToken);
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["durationMs"] = stopwatch.ElapsedMilliseconds,
                    ["response"] = response
                };

                return HealthCheckResult.Healthy("Legacy TMS reachable.", data);
            }
            catch (TmsProtocolException ex)
            {
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["durationMs"] = stopwatch.ElapsedMilliseconds,
                    ["code"] = ex.Code
                };

                return HealthCheckResult.Unhealthy($"Legacy TMS probe failed ({ex.Code}).", ex, data);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                stopwatch.Stop();
                return HealthCheckResult.Unhealthy("Legacy TMS probe failed.", ex);
            }
        }
    }
}
