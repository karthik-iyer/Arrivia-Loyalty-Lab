using LoyaltyLab.Application.Opportunity;
using LoyaltyLab.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Api.Workers;

/// <summary>
/// Batched opportunity scan. <see cref="ScanOpportunities"/> is invokable so a demo
/// can trigger it rather than wait; this worker is the always-on path (FR-O-11).
/// </summary>
public sealed class OpportunityScanWorker(
    IServiceScopeFactory scopes,
    IOptions<OpportunityScanOptions> options,
    ILogger<OpportunityScanWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogCycleFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3, nameof(OpportunityScanWorker)),
            "Opportunity scan cycle failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        var delay = TimeSpan.FromMilliseconds(Math.Max(50, settings.PollIntervalMs));
        var batch = Math.Max(1, settings.BatchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var scan = scope.ServiceProvider.GetRequiredService<ScanOpportunities>();
                await scan.ExecuteAsync(new ScanOpportunitiesCommand(batch), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogCycleFailed(logger, ex);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
