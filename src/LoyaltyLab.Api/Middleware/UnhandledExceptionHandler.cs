using Microsoft.AspNetCore.Diagnostics;

namespace LoyaltyLab.Api.Middleware;

/// <summary>
/// Maps unexpected exceptions to RFC 7807. Expected failures use <c>Result</c> and never reach here.
/// </summary>
internal sealed class UnhandledExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        cancellationToken.ThrowIfCancellationRequested();

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        await Results.Problem(
            title: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError,
            type: "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            extensions: new Dictionary<string, object?>
            {
                ["correlationId"] = httpContext.TraceIdentifier,
            }).ExecuteAsync(httpContext);

        return true;
    }
}
