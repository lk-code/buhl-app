using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests the <c>If-Match</c> header that <c>PUT</c>, <c>PATCH</c> and <c>DELETE</c> require, and the order of the
/// checks: 415 (media type), 404 (unknown id), 428 (no <c>If-Match</c>), 400 (invalid <c>If-Match</c>), 412 (no
/// matching version), 400 (invalid body) and, for <c>PATCH</c>, 409 (failed <c>test</c>).
/// </summary>
public class AddressPreconditionTests
{
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string UnknownId = "0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00";

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

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_MissingIfMatch_Returns428WithProblemDetails(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id);

        // Assert
        await AssertPreconditionRequiredAsync(response);
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_MissingIfMatch_DoesNotChangeAddress(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [TestCaseSource(nameof(InvalidIfMatchCases))]
    public async Task ChangeAsync_InvalidIfMatch_Returns400WithErrorForIfMatch(HttpMethod method, string ifMatch)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, ifMatch);

        // Assert
        await AssertInvalidIfMatchAsync(response);
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_InvalidIfMatch_DoesNotChangeAddress(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, "1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [TestCaseSource(nameof(NotMatchingIfMatchCases))]
    public async Task ChangeAsync_IfMatchWithoutCurrentVersion_Returns412WithProblemDetails(
        HttpMethod method,
        string ifMatch)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, ifMatch);

        // Assert
        await AssertPreconditionFailedAsync(response);
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_IfMatchWithoutCurrentVersion_DoesNotChangeAddress(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, "\"2\"");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_ETagBeforeLastChange_Returns412(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using HttpResponseMessage change = await _client.SendValidAsync(HttpMethod.Put, created.Id, created.ToETag());

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, created.ToETag());

        // Assert
        change.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertPreconditionFailedAsync(response);
    }

    [TestCaseSource(nameof(MatchingIfMatchCases))]
    public async Task ChangeAsync_IfMatchWithCurrentVersion_Succeeds(HttpMethod method, string ifMatch)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, ifMatch);

        // Assert
        response.StatusCode.Should().Be(AddressChangeRequests.GetSuccessStatusCode(method));
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_CurrentVersionInSecondIfMatchHeader_Succeeds(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, "\"0\"", "\"1\"");

        // Assert
        response.StatusCode.Should().Be(AddressChangeRequests.GetSuccessStatusCode(method));
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_ETagOfLastChange_Succeeds(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using HttpResponseMessage change = await _client.SendValidAsync(HttpMethod.Put, created.Id, created.ToETag());
        AddressResponse changed = (await change.Content.ReadFromJsonAsync<AddressResponse>())!;

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, changed.ToETag());

        // Assert
        response.StatusCode.Should().Be(AddressChangeRequests.GetSuccessStatusCode(method));
    }

    [TestCaseSource(nameof(UnknownIdCases))]
    public async Task ChangeAsync_UnknownIdWithAnyIfMatch_Returns404(HttpMethod method, string? ifMatch)
    {
        // Act
        using HttpResponseMessage response = await _client.SendWithIfMatchAsync(
            method,
            $"{AddressChangeRequests.Path}/{UnknownId}",
            AddressChangeRequests.CreateValidContent(method),
            ifMatch);

        // Assert
        await AssertNotFoundAsync(response);
    }

    [TestCaseSource(nameof(UnknownIdCases))]
    public async Task ChangeAsync_DeletedAddressWithAnyIfMatch_Returns404(HttpMethod method, string? ifMatch)
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using HttpResponseMessage deletion =
            await _client.SendValidAsync(HttpMethod.Delete, created.Id, created.ToETag());

        // Act
        using HttpResponseMessage response = await _client.SendWithIfMatchAsync(
            method,
            $"{AddressChangeRequests.Path}/{created.Id}",
            AddressChangeRequests.CreateValidContent(method),
            ifMatch);

        // Assert
        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNotFoundAsync(response);
    }

    [TestCaseSource(nameof(InvalidBodyCases))]
    public async Task ChangeAsync_MissingIfMatchAndInvalidBody_Returns428(HttpMethod method, string body)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await SendAsync(method, created.Id, body, ifMatch: null);

        // Assert
        await AssertPreconditionRequiredAsync(response);
    }

    [TestCaseSource(nameof(InvalidBodyCases))]
    public async Task ChangeAsync_InvalidIfMatchAndInvalidBody_Returns400WithErrorForIfMatchOnly(
        HttpMethod method,
        string body)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await SendAsync(method, created.Id, body, "1");

        // Assert
        await AssertInvalidIfMatchAsync(response);
    }

    [TestCaseSource(nameof(InvalidBodyCases))]
    public async Task ChangeAsync_IfMatchWithoutCurrentVersionAndInvalidBody_Returns412(HttpMethod method, string body)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await SendAsync(method, created.Id, body, "\"2\"");

        // Assert
        await AssertPreconditionFailedAsync(response);
    }

    [TestCaseSource(nameof(InvalidBodyCases))]
    public async Task ChangeAsync_IfMatchWithCurrentVersionAndInvalidBody_Returns400ForBody(
        HttpMethod method,
        string body)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await SendAsync(method, created.Id, body, created.ToETag());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").EnumerateObject().Select(error => error.Name)
            .Should().NotBeEmpty().And.NotContain("If-Match");
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.Methods))]
    public async Task ChangeAsync_InvalidIfMatchWithOtherVersion_Returns400(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, "\"2\", 1");

        // Assert
        await AssertInvalidIfMatchAsync(response);
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.MethodsWithBody))]
    public async Task ChangeAsync_UnsupportedMediaTypeWithoutIfMatch_Returns415(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using var content = new StringContent(AddressPayloads.Updated().ToJsonString());

        // Act
        using HttpResponseMessage response = await _client.SendWithIfMatchAsync(
            method,
            $"{AddressChangeRequests.Path}/{created.Id}",
            content,
            ifMatch: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Test]
    public async Task PatchAsync_IfMatchWithoutCurrentVersionAndFailedTest_Returns412()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Patch,
            created.Id,
            """[{"op": "test", "path": "/city", "value": "Berlin"}]""",
            "\"2\"");

        // Assert
        await AssertPreconditionFailedAsync(response);
    }

    [Test]
    public async Task PatchAsync_IfMatchWithCurrentVersionAndFailedTest_Returns409()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Patch,
            created.Id,
            """[{"op": "test", "path": "/city", "value": "Berlin"}]""",
            created.ToETag());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static IEnumerable<TestCaseData> InvalidIfMatchCases()
    {
        string[] values = ["", " ", "1", "\"1", "1\"", "'1'", "\"1\" x", "W/1", "W/*", "*, \"1\"", "\"1\", *"];

        return Combine(AddressChangeRequests.Methods, values);
    }

    private static IEnumerable<TestCaseData> NotMatchingIfMatchCases()
    {
        string[] values =
        [
            "\"0\"",
            "\"2\"",
            "\"01\"",
            "\"\"",
            "\"abc\"",
            "W/\"1\"",
            "W/\"0\", W/\"1\"",
            "\"2\", \"3\"",
        ];

        return Combine(AddressChangeRequests.Methods, values);
    }

    private static IEnumerable<TestCaseData> MatchingIfMatchCases()
    {
        string[] values = ["\"1\"", "*", "\"0\", \"1\"", "\"1\", \"2\"", "W/\"1\", \"1\""];

        return Combine(AddressChangeRequests.Methods, values);
    }

    private static IEnumerable<TestCaseData> UnknownIdCases()
    {
        string?[] values = [null, "1", "*", "\"1\"", "\"2\""];

        return Combine(AddressChangeRequests.Methods, values);
    }

    /// <summary>
    /// Bodies that are no valid JSON, no valid JSON Patch document or contain invalid values.
    /// </summary>
    private static IEnumerable<TestCaseData> InvalidBodyCases()
    {
        string[] putBodies = ["", "{", "[]", """{"firstName": 42}""", """{"country": "XX"}"""];
        string[] patchBodies =
        [
            "",
            "[",
            "{}",
            """[{"op": "foo", "path": "/city"}]""",
            """[{"op": "replace", "path": "/unknown", "value": "Max"}]""",
            """[{"op": "remove", "path": "/firstName"}]""",
        ];

        return Combine([HttpMethod.Put], putBodies).Concat(Combine([HttpMethod.Patch], patchBodies));
    }

    private static IEnumerable<TestCaseData> Combine(IEnumerable<HttpMethod> methods, IEnumerable<string?> values) =>
        from method in methods
        from value in values
        select new TestCaseData(method, value).SetArgDisplayNames(
            method.Method,
            value is null ? "null" : $"'{value}'");

    private static async Task AssertPreconditionRequiredAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        response.Headers.ETag.Should().BeNull();
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc6585#section-3");
        body.GetProperty("title").GetString().Should().Be("Precondition Required");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.PreconditionRequired);
        body.GetProperty("detail").GetString().Should().Be(
            "The request must contain the header If-Match with the current ETag of the address or '*'.");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task AssertInvalidIfMatchAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.ETag.Should().BeNull();
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        body.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.BadRequest);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        JsonProperty error = body.GetProperty("errors").EnumerateObject().Should().ContainSingle().Which;
        error.Name.Should().Be("If-Match");
        error.Value.EnumerateArray().Select(message => message.GetString()).Should().Equal(
            "The header If-Match must be '*' or a comma-separated list of entity tags, e.g. '\"3\"'.");
    }

    private static async Task AssertPreconditionFailedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        response.Headers.ETag.Should().BeNull();
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.13");
        body.GetProperty("title").GetString().Should().Be("Precondition Failed");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.PreconditionFailed);
        body.GetProperty("detail").GetString().Should().Be(
            "The version of the address does not match the header If-Match. Get the address again and repeat the "
            + "request with its current ETag.");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Not Found");
    }

    private Task<AddressResponse> SeedAsync() => _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, Guid id, string body, string? ifMatch) =>
        _client.SendWithIfMatchAsync(
            method,
            $"{AddressChangeRequests.Path}/{id}",
            AddressChangeRequests.CreateContent(method, body),
            ifMatch);
}
