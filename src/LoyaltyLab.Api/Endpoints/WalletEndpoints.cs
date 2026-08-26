using LoyaltyLab.Api.Http;
using LoyaltyLab.Application.Loyalty;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Api.Endpoints;

internal static class WalletEndpoints
{
    public static void MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/wallet/balance", BalanceAsync)
            .WithTags("Wallet")
            .WithSummary("Member credit balance and monetary value.")
            .Produces<WalletBalanceHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet("/api/wallet/statement", StatementAsync)
            .WithTags("Wallet")
            .WithSummary("Member ledger statement.")
            .Produces<WalletStatementHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet("/api/reports/liability", LiabilityAsync)
            .WithTags("Wallet")
            .WithSummary("Outstanding credit liability as of a date. Operator.")
            .Produces<LiabilityHttp>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> BalanceAsync(
        GetBalance getBalance,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await getBalance.ExecuteAsync(new GetBalanceQuery(), cancellationToken);
        return result.Match(
            balance => Results.Ok(WalletBalanceHttp.From(balance)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> StatementAsync(
        GetStatement getStatement,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await getStatement.ExecuteAsync(new GetStatementQuery(), cancellationToken);
        return result.Match(
            statement => Results.Ok(WalletStatementHttp.From(statement)),
            error => ProblemResults.FromError(http, error));
    }

    private static async Task<IResult> LiabilityAsync(
        DateOnly? asOf,
        GetLiabilityReport report,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var date = asOf ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var result = await report.ExecuteAsync(new GetLiabilityReportQuery(date), cancellationToken);
        return result.Match(
            liability => Results.Ok(LiabilityHttp.From(liability)),
            error => ProblemResults.FromError(http, error));
    }
}

internal sealed record WalletBalanceHttp(Guid MemberId, int Credits, MoneyHttp MonetaryValue, decimal BurnCap)
{
    public static WalletBalanceHttp From(MemberBalance balance) =>
        new(balance.MemberId.Value, balance.Credits, MoneyHttp.From(balance.MonetaryValue), balance.BurnCap.Value);
}

internal sealed record StatementLineHttp(
    Guid Id,
    LedgerTransactionType Type,
    DateTimeOffset OccurredAt,
    string Reason,
    int Credits,
    int RunningBalance,
    Guid? ReversesTransactionId)
{
    public static StatementLineHttp From(StatementLine line) =>
        new(
            line.Id.Value,
            line.Type,
            line.OccurredAt,
            line.Reason,
            line.Credits,
            line.RunningBalance,
            line.ReversesTransactionId?.Value);
}

internal sealed record WalletStatementHttp(Guid MemberId, int Balance, IReadOnlyList<StatementLineHttp> Lines)
{
    public static WalletStatementHttp From(MemberStatement statement) =>
        new(statement.MemberId.Value, statement.Balance, statement.Lines.Select(StatementLineHttp.From).ToArray());
}

internal sealed record LiabilityHttp(
    Guid PartnerId,
    DateOnly AsOf,
    int CreditsIssued,
    int CreditsBurned,
    int CreditsExpired,
    int CreditsOutstanding,
    MoneyHttp MonetaryLiability)
{
    public static LiabilityHttp From(LiabilityReport report) =>
        new(
            report.PartnerId.Value,
            report.AsOf,
            report.CreditsIssued,
            report.CreditsBurned,
            report.CreditsExpired,
            report.CreditsOutstanding,
            MoneyHttp.From(report.MonetaryLiability));
}
