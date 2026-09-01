using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MX.TravelItinerary.Web.Tests;

public sealed class ApplicationStartupTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task IndexLoadsWhenApplicationStarts()
    {
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                [
                    new("AzureAd:ClientId", "test-client-id"),
                    new("AzureAd:TenantId", "test-tenant-id"),
                    new("Storage:TableEndpoint", "https://test.table.core.windows.net/")
                ]));
        }).CreateClient();

        var response = await client.GetAsync("/");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
