using LoyaltyLab.Domain.Opportunity;
using Microsoft.Extensions.Logging;

namespace LoyaltyLab.Application.Opportunity;

internal static partial class OpportunityLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Opportunity suppressed for {Member} ({MemberId}): {Reason}")]
    public static partial void NudgeSuppressed(
        ILogger logger,
        string member,
        Guid memberId,
        SuppressionReason? reason);
}
