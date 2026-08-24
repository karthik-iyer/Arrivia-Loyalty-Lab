using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Api.Http;

internal static class ProblemResults
{
    public static IResult FromError(HttpContext http, Error error)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(error);

        var status = StatusCode(error);
        return Results.Problem(
            title: error.Message,
            statusCode: status,
            type: TypeFor(status),
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["correlationId"] = http.TraceIdentifier,
            });
    }

    private static int StatusCode(Error error)
    {
        if (error == Errors.OfferNotFound
            || error == Errors.QuoteNotFound
            || error == Errors.MemberNotFound
            || error == Errors.LedgerTransactionNotFound
            || error == Errors.PaymentNotFound)
        {
            return StatusCodes.Status404NotFound;
        }

        if (error == Errors.OfferNotEligible
            || error == Errors.BurnCapExceeded
            || error == Errors.InsufficientCredits)
        {
            return StatusCodes.Status422UnprocessableEntity;
        }

        if (error == Errors.QuoteExpired
            || error == Errors.RateChanged
            || error == Errors.IdempotencyKeyReused
            || error == Errors.TransactionAlreadyReversed)
        {
            return StatusCodes.Status409Conflict;
        }

        if (error == Errors.PartnerNotResolved)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (error == Errors.PaymentDeclined)
        {
            return StatusCodes.Status402PaymentRequired;
        }

        if (error == Errors.RoleNotPermitted)
        {
            return StatusCodes.Status403Forbidden;
        }

        return StatusCodes.Status400BadRequest;
    }

    private static string TypeFor(int status) => status switch
    {
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status402PaymentRequired => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status422UnprocessableEntity => "https://tools.ietf.org/html/rfc4918#section-11.2",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    };
}
