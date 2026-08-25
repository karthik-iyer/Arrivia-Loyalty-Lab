using LoyaltyLab.Application.Booking;
using LoyaltyLab.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Api.Workers;

/// <summary>
/// Resumes stalled sagas. <see cref="RecoverStalledSagas"/> is invokable so T-039
/// can expose it on demand; this worker is the always-on path.
/// </summary>
public sealed class SagaRecoveryWorker(
    IServiceScopeFactory scopes,
    IOptions<SagaRecoveryOptions> options,
    ILogger<SagaRecoveryWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogCycleFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(SagaRecoveryWorker)),
            "Saga recovery cycle failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        var delay = TimeSpan.FromMilliseconds(Math.Max(50, settings.PollIntervalMs));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var recover = scope.ServiceProvider.GetRequiredService<RecoverStalledSagas>();
                await recover.ExecuteAsync(stoppingToken);
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
