using System.Net;
using System.Text.Json;
using LoyaltyLab.Api.Tests.Hosting;

namespace LoyaltyLab.Api.Tests.OpenApi;

public sealed class OpenApiDocumentTests : IClassFixture<LoyaltyLabApiFactory>
{
    private readonly LoyaltyLabApiFactory _factory;

    public OpenApiDocumentTests(LoyaltyLabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApi_document_is_anonymous_and_lists_the_http_surface()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/offers", out _).Should().BeTrue();
        paths.TryGetProperty("/api/inbox", out _).Should().BeTrue();
        paths.TryGetProperty("/api/bookings", out _).Should().BeTrue();
        paths.TryGetProperty("/api/concierge/recommend", out _).Should().BeTrue();
        paths.TryGetProperty("/api/wallet/balance", out _).Should().BeTrue();
        paths.TryGetProperty("/health", out _).Should().BeTrue();

        body.Should().Contain("X-Partner-Code");
        body.Should().Contain("X-Member-Id");
        body.Should().Contain("X-Access-Role");
        body.Should().NotContain("\"netRate\"");
    }

    [Fact]
    public async Task Scalar_ui_is_anonymous()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
