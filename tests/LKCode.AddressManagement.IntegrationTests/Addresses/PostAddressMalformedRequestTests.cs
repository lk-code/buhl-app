using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests requests to create an address whose body is no valid JSON object of an address or has an unsupported
/// media type.
/// </summary>
public class PostAddressMalformedRequestTests
{
    private const string Path = "/api/addresses";
    private const string JsonMediaType = "application/json";
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string InvalidInputMessage = "The input was not valid.";
    private const string EmptyBodyMessage = "A non-empty request body is required.";

    private AddressManagementWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _client = _factory.CreateClient();
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
    public async Task PostAsync_InvalidJson_Returns400WithValidationProblemDetails(string json)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(json, JsonMediaType);

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
    public async Task PostAsync_InvalidJson_ReturnsOnlyGenericErrorMessages(string json)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(json, JsonMediaType);

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
    public async Task PostAsync_InvalidJson_DoesNotExposeInternalDetails(string json)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(json, JsonMediaType);

        // Assert
        string text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("LKCode")
            .And.NotContain("CreateAddressRequest")
            .And.NotContain("System.")
            .And.NotContain("LineNumber")
            .And.NotContain("BytePositionInLine");
    }

    [TestCase("{\"firstName\": 42}", "$.firstName")]
    [TestCase("{\"firstName\": \"Erika\", \"email\": true}", "$.email")]
    [TestCase("[]", "$")]
    public async Task PostAsync_ValueOfWrongType_Returns400ForJsonPath(string json, string expectedKey)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(json, JsonMediaType);

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").EnumerateObject().Select(error => error.Name).Should().Equal(expectedKey);
    }

    [TestCase("")]
    [TestCase("null")]
    public async Task PostAsync_EmptyBody_Returns400WithEmptyBodyError(string json)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(json, JsonMediaType);

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
    public async Task PostAsync_JsonMediaType_Returns201(string mediaType)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.Valid().ToJsonString(), mediaType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [TestCase("text/plain")]
    [TestCase("text/json")]
    [TestCase("application/xml")]
    [TestCase("application/x-www-form-urlencoded")]
    public async Task PostAsync_UnsupportedMediaType_Returns415WithProblemDetails(string mediaType)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.Valid().ToJsonString(), mediaType);

        // Assert
        await AssertUnsupportedMediaTypeAsync(response);
    }

    [Test]
    public async Task PostAsync_WithoutContentType_Returns415WithProblemDetails()
    {
        // Arrange
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(AddressPayloads.Valid().ToJsonString()));

        // Act
        using HttpResponseMessage response = await _client.PostAsync(Path, content);

        // Assert
        content.Headers.ContentType.Should().BeNull();
        await AssertUnsupportedMediaTypeAsync(response);
    }

    [Test]
    public async Task PostAsync_MalformedRequest_DoesNotStoreAddress()
    {
        // Arrange
        int countBefore = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage invalidJson = await PostAsync("{\"firstName\": 42}", JsonMediaType);
        using HttpResponseMessage unsupportedMediaType =
            await PostAsync(AddressPayloads.Valid().ToJsonString(), "text/plain");

        // Assert
        invalidJson.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unsupportedMediaType.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await _factory.ListAddressesAsync()).Should().HaveCount(countBefore);
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

    private async Task<HttpResponseMessage> PostAsync(string body, string mediaType)
    {
        using var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);

        return await _client.PostAsync(Path, content);
    }
}
