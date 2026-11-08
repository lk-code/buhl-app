using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests requests to change an address whose body is no valid JSON object of an address or has an unsupported
/// media type.
/// </summary>
public class PutAddressMalformedRequestTests
{
    private const string Path = "/api/addresses";
    private const string JsonMediaType = "application/json";
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string InvalidInputMessage = "The input was not valid.";
    private const string EmptyBodyMessage = "A non-empty request body is required.";

    private AddressManagementWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _id;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _client = _factory.CreateClient();
        _id = (await _factory.SeedAddressAsync(AddressPayloads.ValidRequest)).Id;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestCase("   ")]
    [TestCase("{")]
    [TestCase("not json")]
    [TestCase("{\"firstName\": }")]
    [TestCase("{\"firstName\": \"Erika\",}")]
    [TestCase("{'firstName': 'Erika'}")]
    [TestCase("[]")]
    [TestCase("\"Erika\"")]
    [TestCase("42")]
    [TestCase("{\"firstName\": 42}")]
    [TestCase("{\"email\": true}")]
    [TestCase("{\"country\": [\"DE\"]}")]
    public async Task PutAsync_InvalidJson_Returns400WithValidationProblemDetails(string json)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(_id, json, JsonMediaType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        body.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.BadRequest);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [TestCase("   ")]
    [TestCase("{")]
    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("\"Erika\"")]
    [TestCase("{\"firstName\": 42}")]
    [TestCase("{\"email\": true}")]
    public async Task PutAsync_InvalidJson_ReturnsOnlyGenericErrorMessages(string json)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(_id, json, JsonMediaType);

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").EnumerateObject()
            .SelectMany(error => error.Value.EnumerateArray())
            .Select(message => message.GetString())
            .Should().NotBeEmpty().And.AllBe(InvalidInputMessage);
    }

    [TestCase("[]")]
    [TestCase("{\"firstName\": 42}")]
    [TestCase("{\"firstName\": \"Erika\", \"lastName\": }")]
    public async Task PutAsync_InvalidJson_DoesNotExposeInternalDetails(string json)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(_id, json, JsonMediaType);

        // Assert
        string text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("LKCode")
            .And.NotContain("UpdateAddressRequest")
            .And.NotContain("System.")
            .And.NotContain("LineNumber")
            .And.NotContain("BytePositionInLine");
    }

    [TestCase("{\"firstName\": 42}", "$.firstName")]
    [TestCase("{\"firstName\": \"Erika\", \"email\": true}", "$.email")]
    [TestCase("[]", "$")]
    public async Task PutAsync_ValueOfWrongType_Returns400ForJsonPath(string json, string expectedKey)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(_id, json, JsonMediaType);

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").EnumerateObject().Select(error => error.Name).Should().Equal(expectedKey);
    }

    [TestCase("")]
    [TestCase("null")]
    public async Task PutAsync_EmptyBody_Returns400WithEmptyBodyError(string json)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(_id, json, JsonMediaType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonProperty error = body.GetProperty("errors").EnumerateObject().Should().ContainSingle().Which;
        error.Name.Should().BeEmpty();
        error.Value.EnumerateArray().Select(message => message.GetString()).Should().Equal(EmptyBodyMessage);
    }

    [TestCase("application/json")]
    [TestCase("application/json; charset=utf-8")]
    [TestCase("Application/JSON")]
    public async Task PutAsync_JsonMediaType_Returns200(string mediaType)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(_id, AddressPayloads.Valid().ToJsonString(), mediaType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase("text/plain")]
    [TestCase("text/json")]
    [TestCase("application/xml")]
    [TestCase("application/x-www-form-urlencoded")]
    public async Task PutAsync_UnsupportedMediaType_Returns415WithProblemDetails(string mediaType)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(_id, AddressPayloads.Valid().ToJsonString(), mediaType);

        // Assert
        await AssertUnsupportedMediaTypeAsync(response);
    }

    [Test]
    public async Task PutAsync_WithoutContentType_Returns415WithProblemDetails()
    {
        // Arrange
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(AddressPayloads.Valid().ToJsonString()));

        // Act
        using HttpResponseMessage response = await _client.PutAsync($"{Path}/{_id}", content);

        // Assert
        content.Headers.ContentType.Should().BeNull();
        await AssertUnsupportedMediaTypeAsync(response);
    }

    [Test]
    public async Task PutAsync_MalformedRequest_DoesNotChangeAddress()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage invalidJson = await PutAsync(created.Id, "{\"firstName\": 42}", JsonMediaType);
        using HttpResponseMessage unsupportedMediaType =
            await PutAsync(created.Id, AddressPayloads.Updated().ToJsonString(), "text/plain");

        // Assert
        invalidJson.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unsupportedMediaType.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    private static async Task AssertUnsupportedMediaTypeAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.16");
        body.GetProperty("title").GetString().Should().Be("Unsupported Media Type");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.UnsupportedMediaType);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private async Task<HttpResponseMessage> PutAsync(Guid id, string body, string mediaType)
    {
        using var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);

        return await _client.PutAsync($"{Path}/{id}", content);
    }
}
