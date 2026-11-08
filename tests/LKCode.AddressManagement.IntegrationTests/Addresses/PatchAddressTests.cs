using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests changing single values of an address with a JSON Patch document.
/// </summary>
public class PatchAddressTests
{
    private const string Path = "/api/addresses";
    private const string JsonPatchMediaType = "application/json-patch+json";
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string UnknownId = "0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00";

    private static readonly JsonSerializerOptions _webOptions = JsonSerializerOptions.Web;

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

    [TestCase("firstName", "Max")]
    [TestCase("lastName", "Musterfrau")]
    [TestCase("street", "Hauptstraße")]
    [TestCase("houseNumber", "1a")]
    [TestCase("postalCode", "10115")]
    [TestCase("city", "Berlin")]
    [TestCase("country", "AT")]
    [TestCase("email", "max.musterfrau@example.org")]
    public async Task PatchAsync_ReplaceValue_Returns200WithOnlyThisValueChanged(string field, string value)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", field, value));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        await AssertValuesAsync(response, created, (field, value));
    }

    [Test]
    public async Task PatchAsync_ValidPatch_KeepsIdAndCreatedAtAndSetsUpdatedAt()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", "city", "Berlin"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().BeNull();
        AddressResponse body = (await response.Content.ReadFromJsonAsync<AddressResponse>())!;
        body.Id.Should().Be(created.Id);
        body.CreatedAt.Should().Be(created.CreatedAt);
        body.UpdatedAt.Should().BeOnOrAfter(created.UpdatedAt)
            .And.BeCloseTo(TimeProvider.System.GetUtcNow(), TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task PatchAsync_ValidPatch_AddressCanBeReadWithNewValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", "city", "Berlin"));
        AddressResponse? patched = await response.Content.ReadFromJsonAsync<AddressResponse>();

        // Act
        using HttpResponseMessage getResponse = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse? read = await getResponse.Content.ReadFromJsonAsync<AddressResponse>();
        read.Should().Be(patched);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(patched);
    }

    [Test]
    public async Task PatchAsync_ValidPatch_ChangesOnlyThisAddress()
    {
        // Arrange
        AddressResponse changed = await SeedAsync();
        AddressResponse other = await SeedAsync();
        int countBefore = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await PatchAsync(changed, Operation("replace", "city", "Berlin"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _factory.FindAddressAsync(other.Id)).Should().Be(other);
        (await _factory.ListAddressesAsync()).Should().HaveCount(countBefore);
    }

    [Test]
    public async Task PatchAsync_AddEmailToAddressWithoutEmail_AddsEmail()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest with { Email = null });

        // Act
        using HttpResponseMessage response =
            await PatchAsync(created, Operation("add", "email", "erika@example.com"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("email", "erika@example.com"));
    }

    [Test]
    public async Task PatchAsync_AddExistingValue_ReplacesValue()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("add", "firstName", "Max"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("firstName", "Max"));
    }

    [Test]
    public async Task PatchAsync_RemoveEmail_RemovesEmail()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("remove", "email"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("email", null));
        (await _factory.FindAddressAsync(created.Id))!.Email.Should().BeNull();
    }

    [Test]
    public async Task PatchAsync_RemoveMissingEmail_Returns200()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest with { Email = null });

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("remove", "email"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created);
    }

    [Test]
    public async Task PatchAsync_SuccessfulTest_AppliesOperations()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("test", "firstName", "Erika"),
            Operation("test", "email", "erika.mustermann@example.com"),
            Operation("replace", "firstName", "Max"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("firstName", "Max"));
    }

    [Test]
    public async Task PatchAsync_TestAfterReplace_ComparesWithChangedValue()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("replace", "firstName", "Max"),
            Operation("test", "firstName", "Max"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("firstName", "Max"));
    }

    [Test]
    public async Task PatchAsync_TestOfMissingEmail_Returns200()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest with { Email = null });

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("test", "email", null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created);
    }

    [Test]
    public async Task PatchAsync_OnlyTest_Returns200WithUnchangedValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("test", "city", "Köln"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created);
    }

    [Test]
    public async Task PatchAsync_Copy_CopiesValue()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, OperationFrom("copy", "street", "city"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("street", "Köln"));
    }

    [Test]
    public async Task PatchAsync_MoveEmail_MovesValueAndRemovesEmail()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await PatchAsync(created, OperationFrom("move", "lastName", "email"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("lastName", "erika.mustermann@example.com"), ("email", null));
    }

    [Test]
    public async Task PatchAsync_MoveRequiredValue_Returns400ForMovedValue()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await PatchAsync(created, OperationFrom("move", "houseNumber", "postalCode"));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal("postalCode");
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [Test]
    public async Task PatchAsync_SeveralOperations_AppliesAllInOrder()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("replace", "firstName", "Max"),
            Operation("replace", "lastName", "Musterfrau"),
            OperationFrom("copy", "city", "firstName"),
            Operation("replace", "firstName", "Moritz"),
            Operation("remove", "email"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(
            response,
            created,
            ("firstName", "Moritz"),
            ("lastName", "Musterfrau"),
            ("city", "Max"),
            ("email", null));
    }

    [Test]
    public async Task PatchAsync_NoOperations_Returns200WithUnchangedValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created);
    }

    [Test]
    public async Task PatchAsync_ValuesWithSurroundingWhitespace_ReturnsTrimmedValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("replace", "firstName", "  Max "),
            Operation("replace", "postalCode", "\t10115"),
            Operation("add", "email", " max@example.org  "));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(
            response,
            created,
            ("firstName", "Max"),
            ("postalCode", "10115"),
            ("email", "max@example.org"));
    }

    [TestCase("at")]
    [TestCase("At")]
    [TestCase(" at ")]
    public async Task PatchAsync_CountryNotUppercase_ReturnsUppercaseCountry(string country)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", "country", country));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("country", "AT"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public async Task PatchAsync_BlankEmail_RemovesEmail(string? email)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", "email", email));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, ("email", null));
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.Fields))]
    public async Task PatchAsync_ValueAtMaxLength_Returns200WithValue(AddressPayloadField field)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await PatchAsync(created, Operation("replace", field.Name, field.ValueAtMaxLength));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertValuesAsync(response, created, (field.Name, field.ValueAtMaxLength));
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.RequiredFields))]
    public async Task PatchAsync_RemoveRequiredValue_Returns400ForValue(AddressPayloadField field)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("remove", field.Name));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [TestCaseSource(nameof(BlankRequiredValues))]
    public async Task PatchAsync_RequiredValueBlank_Returns400ForValue(AddressPayloadField field, string? value)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", field.Name, value));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.Fields))]
    public async Task PatchAsync_ValueExceedsMaxLength_Returns400ForValue(AddressPayloadField field)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await PatchAsync(created, Operation("add", field.Name, field.ValueExceedingMaxLength));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [TestCase("GER")]
    [TestCase("D")]
    [TestCase("de1")]
    [TestCase("XX")]
    [TestCase("UK")]
    [TestCase("D E")]
    public async Task PatchAsync_InvalidCountry_Returns400ForCountry(string country)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", "country", country));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal("country");
    }

    [TestCase("erika")]
    [TestCase("erika.example.com")]
    [TestCase("@example.com")]
    [TestCase("erika@")]
    [TestCase("erika mustermann@example.com")]
    public async Task PatchAsync_InvalidEmail_Returns400ForEmail(string email)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("add", "email", email));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal("email");
    }

    [Test]
    public async Task PatchAsync_SeveralInvalidValues_Returns400WithErrorForEachValue()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("remove", "firstName"),
            Operation("replace", "country", "XX"),
            Operation("replace", "email", "erika"));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().BeEquivalentTo("firstName", "country", "email");
    }

    [Test]
    public async Task PatchAsync_InvalidValue_ReturnsValidationProblemDetails()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", "country", "XX"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("type", "title", "status", "errors", "traceId");
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        body.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.BadRequest);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("errors").GetProperty("country").EnumerateArray().Select(error => error.GetString())
            .Should().Equal("'Country' must be an assigned ISO 3166-1 alpha-2 country code.");
    }

    [Test]
    public async Task PatchAsync_InvalidValue_DoesNotChangeAddress()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("replace", "city", "Berlin"),
            Operation("replace", "firstName", " "));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [TestCase("""{"op": "foo", "path": "/firstName", "value": "Max"}""", "$[0].op")]
    [TestCase("""{"op": "REPLACE", "path": "/firstName", "value": "Max"}""", "$[0].op")]
    [TestCase("""{"path": "/firstName", "value": "Max"}""", "$[0].op")]
    [TestCase("""{"op": "replace", "value": "Max"}""", "$[0].path")]
    [TestCase("""{"op": "replace", "path": "", "value": "Max"}""", "$[0].path")]
    [TestCase("""{"op": "replace", "path": "firstName", "value": "Max"}""", "$[0].path")]
    [TestCase("""{"op": "replace", "path": "/FirstName", "value": "Max"}""", "$[0].path")]
    [TestCase("""{"op": "replace", "path": "/unknown", "value": "Max"}""", "$[0].path")]
    [TestCase("""{"op": "replace", "path": "/id", "value": "Max"}""", "$[0].path")]
    [TestCase("""{"op": "replace", "path": "/updatedAt", "value": "2026-01-01T00:00:00Z"}""", "$[0].path")]
    [TestCase("""{"op": "remove", "path": "/firstName/0"}""", "$[0].path")]
    [TestCase("""{"op": "test", "path": "/unknown", "value": "Max"}""", "$[0].path")]
    [TestCase("""{"op": "move", "path": "/firstName"}""", "$[0].from")]
    [TestCase("""{"op": "copy", "from": "/unknown", "path": "/firstName"}""", "$[0].from")]
    [TestCase("""{"op": "replace", "path": "/firstName", "value": 42}""", "$[0].value")]
    [TestCase("""{"op": "add", "path": "/email", "value": true}""", "$[0].value")]
    [TestCase("""{"op": "replace", "path": "/country", "value": ["DE"]}""", "$[0].value")]
    [TestCase("""{"op": "test", "path": "/city", "value": {"name": "Köln"}}""", "$[0].value")]
    public async Task PatchAsync_InvalidOperation_Returns400ForJsonPath(string operation, string expectedKey)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(created, JsonNode.Parse(operation)!);

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(expectedKey);
    }

    [Test]
    public async Task PatchAsync_SeveralInvalidOperations_Returns400WithAllErrors()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            JsonNode.Parse("""{"op": "foo", "path": "/firstName"}""")!,
            Operation("replace", "city", "Berlin"),
            JsonNode.Parse("""{"op": "copy", "from": "/Email", "path": "/street"}""")!,
            JsonNode.Parse("""{"op": "test", "path": "/unknown", "value": 1}""")!);

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        errors.EnumerateObject().ToDictionary(
                error => error.Name,
                error => error.Value.EnumerateArray().Select(message => message.GetString()).ToArray())
            .Should().BeEquivalentTo(new Dictionary<string, string[]>
            {
                ["$[0].op"] = ["The operation must be 'add', 'remove', 'replace', 'move', 'copy' or 'test'."],
                ["$[2].from"] =
                    ["The location to move or copy from must point to a field of the address, e.g. '/firstName'."],
                ["$[3].path"] = ["The path must point to a field of the address, e.g. '/firstName'."],
                ["$[3].value"] = ["The value must be a string or null."],
            });
    }

    [Test]
    public async Task PatchAsync_InvalidOperation_DoesNotChangeAddress()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("replace", "city", "Berlin"),
            JsonNode.Parse("""{"op": "replace", "path": "/unknown", "value": "Max"}""")!);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [Test]
    public async Task PatchAsync_UnknownIdAndInvalidOperation_Returns404WithProblemDetails()
    {
        // Act
        using HttpResponseMessage response =
            await PatchAsync(UnknownId, JsonNode.Parse("""{"op": "replace", "path": "/unknown", "value": "Max"}""")!);

        // Assert
        await AssertNotFoundAsync(response);
    }

    [TestCase("firstName", "Max")]
    [TestCase("firstName", "erika")]
    [TestCase("firstName", "Erika ")]
    [TestCase("firstName", null)]
    [TestCase("country", "de")]
    [TestCase("email", null)]
    [TestCase("email", "ERIKA.MUSTERMANN@example.com")]
    public async Task PatchAsync_TestOfDifferentValue_Returns409WithProblemDetails(string field, string? value)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("replace", "city", "Berlin"),
            Operation("test", field, value));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("type", "title", "status", "detail", "traceId");
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.10");
        body.GetProperty("title").GetString().Should().Be("Conflict");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.Conflict);
        body.GetProperty("detail").GetString().Should().Be(
            $"The test operation at index 1 failed: the value at '/{field}' is not equal to the test value.");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task PatchAsync_TestOfMissingValue_Returns409()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await PatchAsync(created, JsonNode.Parse("""{"op": "test", "path": "/email"}""")!);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task PatchAsync_FailedTest_DoesNotChangeAddress()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("replace", "city", "Berlin"),
            Operation("test", "firstName", "Max"),
            Operation("replace", "lastName", "Musterfrau"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [Test]
    public async Task PatchAsync_FailedTestAndInvalidValue_Returns409()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PatchAsync(
            created,
            Operation("remove", "firstName"),
            Operation("test", "city", "Berlin"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestCase(UnknownId)]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public async Task PatchAsync_UnknownId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(id, Operation("replace", "city", "Berlin"));

        // Assert
        await AssertNotFoundAsync(response);
    }

    [Test]
    public async Task PatchAsync_UnknownId_ReturnsSameProblemDetailsAsUnknownRoute()
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(UnknownId, Operation("replace", "city", "Berlin"));
        using HttpResponseMessage unknownRouteResponse =
            await SendPatchAsync("/does-not-exist", "\"1\"", Operation("replace", "city", "Berlin"));

        // Assert
        IReadOnlyDictionary<string, string> body = await ReadWithoutTraceIdAsync(response);
        IReadOnlyDictionary<string, string> unknownRouteBody = await ReadWithoutTraceIdAsync(unknownRouteResponse);
        body.Should().Equal(unknownRouteBody);
    }

    [Test]
    public async Task PatchAsync_UnknownId_DoesNotCreateAddress()
    {
        // Arrange
        int countBefore = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await PatchAsync(UnknownId, Operation("replace", "city", "Berlin"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _factory.FindAddressAsync(Guid.Parse(UnknownId))).Should().BeNull();
        (await _factory.ListAddressesAsync()).Should().HaveCount(countBefore);
    }

    [Test]
    public async Task PatchAsync_UnknownIdAndFailedTest_Returns404()
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(UnknownId, Operation("test", "city", "Berlin"));

        // Assert
        await AssertNotFoundAsync(response);
    }

    [Test]
    public async Task PatchAsync_DeletedAddress_Returns404WithProblemDetails()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using HttpResponseMessage deleteResponse = await _client.SendWithIfMatchAsync(
            HttpMethod.Delete,
            $"{Path}/{created.Id}",
            content: null,
            created.ToETag());

        // Act
        using HttpResponseMessage response = await PatchAsync(created, Operation("replace", "city", "Berlin"));

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNotFoundAsync(response);
        (await _factory.FindAddressAsync(created.Id)).Should().BeNull();
    }

    [TestCase("not-a-guid")]
    [TestCase("123")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f0")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f000")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1fzz")]
    public async Task PatchAsync_InvalidId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await PatchAsync(id, Operation("replace", "city", "Berlin"));

        // Assert
        await AssertNotFoundAsync(response);
    }

    private static IEnumerable<TestCaseData> BlankRequiredValues()
    {
        string?[] values = [null, string.Empty, " ", "   ", "\t\n"];

        return AddressPayloads.RequiredFields.SelectMany(field => values.Select(value =>
            new TestCaseData(field, value).SetArgDisplayNames(field.Name, value is null ? "null" : $"\"{value}\"")));
    }

    private static JsonObject Operation(string op, string field, string? value) => new()
    {
        ["op"] = op,
        ["path"] = "/" + field,
        ["value"] = value,
    };

    private static JsonObject Operation(string op, string field) => new()
    {
        ["op"] = op,
        ["path"] = "/" + field,
    };

    private static JsonObject OperationFrom(string op, string field, string from) => new()
    {
        ["op"] = op,
        ["from"] = "/" + from,
        ["path"] = "/" + field,
    };

    /// <summary>
    /// Asserts that the body of the <paramref name="response"/> contains the values of the
    /// <paramref name="created"/> address with the <paramref name="changes"/> and the version of one change; the
    /// time of the change is ignored.
    /// </summary>
    private static async Task AssertValuesAsync(
        HttpResponseMessage response,
        AddressResponse created,
        params (string Field, string? Value)[] changes)
    {
        JsonObject expected = JsonSerializer.SerializeToNode(created, _webOptions)!.AsObject();
        foreach ((string field, string? value) in changes)
        {
            expected[field] = value;
        }

        expected["version"] = created.Version + 1;

        JsonObject actual = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        expected.Remove("updatedAt");
        actual.Remove("updatedAt");
        actual.ToJsonString().Should().Be(expected.ToJsonString());
    }

    private static async Task<JsonElement> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("errors");
    }

    private static IEnumerable<string> GetKeys(JsonElement errors) =>
        errors.EnumerateObject().Select(error => error.Name);

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().Should().Be("Not Found");
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.5");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadWithoutTraceIdAsync(
        HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .ToDictionary(property => property.Name, property => property.Value.GetRawText());
    }

    private Task<AddressResponse> SeedAsync() => _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

    /// <summary>
    /// Changes the <paramref name="address"/> with the entity tag of its version in the <c>If-Match</c> header.
    /// </summary>
    private Task<HttpResponseMessage> PatchAsync(AddressResponse address, params JsonNode[] operations) =>
        SendPatchAsync($"{Path}/{address.Id}", address.ToETag(), operations);

    private Task<HttpResponseMessage> PatchAsync(string id, params JsonNode[] operations) =>
        SendPatchAsync($"{Path}/{id}", "\"1\"", operations);

    private async Task<HttpResponseMessage> SendPatchAsync(string url, string ifMatch, params JsonNode[] operations)
    {
        using var content = new StringContent(new JsonArray(operations).ToJsonString(), Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(JsonPatchMediaType);

        return await _client.SendWithIfMatchAsync(HttpMethod.Patch, url, content, ifMatch);
    }
}
