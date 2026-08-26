using LoyaltyLab.Api.Middleware;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace LoyaltyLab.Api.OpenApi;

internal static class OpenApiSetup
{
    public static IServiceCollection AddLoyaltyLabOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Loyalty Lab API",
                    Version = "v1",
                    Description =
                        "Partner-scoped HTTP API. Send X-Partner-Code (SUMMIT or NIMBUS). "
                        + "Members send X-Member-Id. Operators send X-Access-Role: Operator. "
                        + "Cross-tenant access returns 404.",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer<TenantHeaderTransformer>();
        });
        return services;
    }

    public static WebApplication MapLoyaltyLabOpenApi(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "Loyalty Lab API";
            options.OpenApiRoutePattern = "/openapi/{documentName}.json";
        });
        return app;
    }
}

internal sealed class TenantHeaderTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = "/" + (context.Description.RelativePath ?? string.Empty);
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        AddHeader(
            operation,
            TenantResolutionMiddleware.PartnerHeader,
            required: true,
            "Partner code: SUMMIT or NIMBUS.");
        AddHeader(
            operation,
            TenantResolutionMiddleware.MemberHeader,
            required: false,
            "Member id for member-scoped routes. Omit for operator/anonymous partner calls.");
        AddHeader(
            operation,
            TenantResolutionMiddleware.RoleHeader,
            required: false,
            "Access role. Use Operator for reports, sagas, and admin workers.");

        if (IsIdempotentWrite(context.Description.HttpMethod, path))
        {
            AddHeader(
                operation,
                "Idempotency-Key",
                required: true,
                "Caller-supplied key so a retried booking write is not applied twice.");
        }

        return Task.CompletedTask;
    }

    private static bool IsIdempotentWrite(string? method, string path) =>
        string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
        && (path.Equals("/api/bookings", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/cancel", StringComparison.OrdinalIgnoreCase));

    private static void AddHeader(OpenApiOperation operation, string name, bool required, string description)
    {
        var parameters = operation.Parameters ??= [];
        if (parameters.Any(parameter =>
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Description = description,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });
    }
}
