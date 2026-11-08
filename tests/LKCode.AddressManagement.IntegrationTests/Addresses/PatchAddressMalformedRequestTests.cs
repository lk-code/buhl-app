using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests requests to change single values of an address whose body is no valid JSON Patch document or has an
/// unsupported media type.
/// </summary>
public class PatchAddressMalformedRequestTests
{
    private const string Path = "/api/addresses";
    private const string JsonPatchMediaType = "application/json-patch+json";
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string InvalidInputMessage = "The input was not valid.";
    private const string EmptyBodyMessage = "A non-empty request body is required.";
    private const string ValidPatch = """[{"op": "replace", "path": "/city", "value": "Berlin"}]""";

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
    [TestCase("[")]
    [TestCase("not json")]
    [TestCase("{}")]
    [TestCase("""{"op": "replace", "path": "/city", "value": "Berlin"}""")]
    [TestCase("\"replace\"")]
    [TestCase("42")]
    [TestCase("[1]")]
    [TestCase("""["replace"]""")]
    [TestCase("[[]]")]
    [TestCase("""[{"op": "replace", "path": 42}]""")]
    [TestCase("""[{"op": 1, "path": "/city"}]""")]
    [TestCase("""[{"op": "copy", "from": ["/city"], "path": "/street"}]""")]
    [TestCase("""[{"op": "replace", "path": "/city", "value": }]""")]
    [TestCase("""[{"op": "replace", "path": "/city", "value": "Berlin"},]""")]
    [TestCase("[{'op': 'replace', 'path': '/city', 'value': 'Berlin'}]")]
    public async Task PatchAsync_InvalidJson_Returns400WithValidationProblemDetails(string json)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(_id, json, JsonPatchMediaType);

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

    [TestCase("[")]
    [TestCase("not json")]
    [TestCase("{}")]
    [TestCase("[1]")]
    [TestCase("""[{"op": "replace", "path": 42}]""")]
    [TestCase("""[{"op": "replace", "path": "/city", "value": }]""")]
    public async Task PatchAsync_InvalidJson_ReturnsOnlyGenericErrorMessages(string json)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(_id, json, JsonPatchMediaType);

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").EnumerateObject()
            .SelectMany(error => error.Value.EnumerateArray())
            .Select(message => message.GetString())
            .Should().NotBeEmpty().And.AllBe(InvalidInputMessage);
    }

    [TestCase("{}")]
    [TestCase("[1]")]
    [TestCase("""[{"op": "replace", "path": 42}]""")]
    [TestCase("""[{"op": "replace", "path": "/city", "value": }]""")]
    public async Task PatchAsync_InvalidJson_DoesNotExposeInternalDetails(string json)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(_id, json, JsonPatchMediaType);

        // Assert
        string text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("LKCode")
            .And.NotContain("UpdateAddressRequest")
            .And.NotContain("JsonPatch")
            .And.NotContain("Operation")
            .And.NotContain("System.")
            .And.NotContain("LineNumber")
            .And.NotContain("BytePositionInLine");
    }

    [TestCase("{}")]
    [TestCase("[1]")]
    [TestCase("""[{"op": "replace", "path": 42}]""")]
    public async Task PatchAsync_NoValidPatchDocument_Returns400ForRoot(string json)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(_id, json, JsonPatchMediaType);

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").EnumerateObject().Select(error => error.Name).Should().Equal("$");
    }

    [TestCase("")]
    [TestCase("null")]
    public async Task PatchAsync_EmptyBody_Returns400WithEmptyBodyError(string json)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(_id, json, JsonPatchMediaType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonProperty error = body.GetProperty("errors").EnumerateObject().Should().ContainSingle().Which;
        error.Name.Should().BeEmpty();
        error.Value.EnumerateArray().Select(message => message.GetString()).Should().Equal(EmptyBodyMessage);
    }

    [TestCase("application/json-patch+json")]
    [TestCase("application/json-patch+json; charset=utf-8")]
    [TestCase("Application/JSON-Patch+JSON")]
    public async Task PatchAsync_JsonPatchMediaType_Returns200(string mediaType)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(_id, ValidPatch, mediaType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase("application/json")]
    [TestCase("application/merge-patch+json")]
    [TestCase("application/problem+json")]
    [TestCase("text/json")]
    [TestCase("text/plain")]
    [TestCase("application/xml")]
    [TestCase("application/x-www-form-urlencoded")]
    public async Task PatchAsync_UnsupportedMediaType_Returns415WithProblemDetails(string mediaType)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(_id, ValidPatch, mediaType);

        // Assert
        await AssertUnsupportedMediaTypeAsync(response);
    }

    [Test]
    public async Task PatchAsync_WithoutContentType_Returns415WithProblemDetails()
    {
        // Arrange
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(ValidPatch));

        // Act
        using HttpResponseMessage response = await _client.PatchAsync($"{Path}/{_id}", content);

        // Assert
        content.Headers.ContentType.Should().BeNull();
        await AssertUnsupportedMediaTypeAsync(response);
    }

    [Test]
    public async Task PatchAsync_UnsupportedMediaTypeAndUnknownId_Returns415()
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(Guid.CreateVersion7(), ValidPatch, "application/json");

        // Assert
        await AssertUnsupportedMediaTypeAsync(response);
    }

    [Test]
    public async Task PatchAsync_MalformedRequest_DoesNotChangeAddress()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage invalidJson =
            await PatchAsync(created.Id, """[{"op": "replace", "path": "/city", "value": }]""", JsonPatchMediaType);
        using HttpResponseMessage unsupportedMediaType = await PatchAsync(created.Id, ValidPatch, "application/json");

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

    private async Task<HttpResponseMessage> PatchAsync(Guid id, string body, string mediaType)
    {
        using var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);

        return await _client.PatchAsync($"{Path}/{id}", content);
    }
}
