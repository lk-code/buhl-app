using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests that the validation messages are English, whatever the culture of the server is.
/// </summary>
public class ValidationMessageCultureTests
{
    private AddressManagementWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _germanFactory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _germanFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddTransient<IStartupFilter, GermanCultureStartupFilter>()));
        _client = _germanFactory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _germanFactory.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task PostAsync_BlankValueOnGermanServer_ReturnsEnglishMessage()
    {
        // Act
        using HttpResponseMessage response =
            await _client.PostAsJsonAsync("/api/addresses", AddressPayloads.ValidWith("firstName", " "));

        // Assert
        (await ReadMessagesAsync(response, "firstName")).Should().Equal("'First Name' must not be empty.");
    }

    [Test]
    public async Task PostAsync_TooLongValueOnGermanServer_ReturnsEnglishMessage()
    {
        // Arrange
        JsonObject payload =
            AddressPayloads.ValidWith("city", new string('a', AddressConstraints.CityMaxLength + 1));

        // Act
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/addresses", payload);

        // Assert
        (await ReadMessagesAsync(response, "city")).Should().Equal(
            "The length of 'City' must be 100 characters or fewer. You entered 101 characters.");
    }

    private static async Task<IEnumerable<string?>> ReadMessagesAsync(HttpResponseMessage response, string name)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("errors").GetProperty(name).EnumerateArray().Select(message => message.GetString());
    }
}
