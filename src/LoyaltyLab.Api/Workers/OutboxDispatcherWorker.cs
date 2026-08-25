using LoyaltyLab.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;

namespace LoyaltyLab.Api.Workers;

/// <summary>
/// Polls the transactional outbox. Dispatch itself is invokable so T-039 can
/// expose it on demand; this worker is the always-on path.
/// </summary>
public sealed class OutboxDispatcherWorker(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogCycleFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(OutboxDispatcherWorker)),
            "Outbox dispatch cycle failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Dispatcher.Enabled)
        {
            return;
        }

        var delay = TimeSpan.FromMilliseconds(Math.Max(50, settings.PollIntervalMs));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                await dispatcher.DispatchAsync(stoppingToken);
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
