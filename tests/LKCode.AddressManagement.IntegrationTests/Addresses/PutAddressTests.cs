using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

public class PutAddressTests
{
    private const string Path = "/api/addresses";
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

    [Test]
    public async Task PutAsync_ValidAddress_Returns200WithNewValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.Updated());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        response.Headers.Location.Should().BeNull();
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body.Should().BeEquivalentTo(AddressPayloads.UpdatedRequest);
        body.Id.Should().Be(created.Id);
        body.CreatedAt.Should().Be(created.CreatedAt);
        body.UpdatedAt.Should().BeOnOrAfter(created.UpdatedAt)
            .And.BeCloseTo(TimeProvider.System.GetUtcNow(), TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task PutAsync_ValidAddress_AddressCanBeReadWithNewValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.Updated());
        AddressResponse? updated = await response.Content.ReadFromJsonAsync<AddressResponse>();

        // Act
        using HttpResponseMessage getResponse = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse? read = await getResponse.Content.ReadFromJsonAsync<AddressResponse>();
        read.Should().Be(updated);
    }

    [Test]
    public async Task PutAsync_ValidAddress_ChangesOnlyThisAddress()
    {
        // Arrange
        AddressResponse changed = await SeedAsync();
        AddressResponse other = await SeedAsync();
        int countBefore = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await PutAsync(changed.Id, AddressPayloads.Updated());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _factory.FindAddressAsync(other.Id)).Should().Be(other);
        (await _factory.ListAddressesAsync()).Should().HaveCount(countBefore);
    }

    [Test]
    public async Task PutAsync_SameAddressTwice_Returns200WithSameValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage first = await PutAsync(created.Id, AddressPayloads.Updated());
        using HttpResponseMessage second = await PutAsync(created.Id, AddressPayloads.Updated());

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse firstBody = (await first.Content.ReadFromJsonAsync<AddressResponse>())!;
        AddressResponse secondBody = (await second.Content.ReadFromJsonAsync<AddressResponse>())!;
        secondBody.Should().BeEquivalentTo(firstBody, options => options.Excluding(address => address.UpdatedAt));
        secondBody.UpdatedAt.Should().BeOnOrAfter(firstBody.UpdatedAt);
    }

    [Test]
    public async Task PutAsync_ValuesWithSurroundingWhitespace_ReturnsTrimmedValues()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        var payload = new JsonObject
        {
            ["firstName"] = "  Max ",
            ["lastName"] = " Musterfrau",
            ["street"] = "Hauptstraße  ",
            ["houseNumber"] = " 1a ",
            ["postalCode"] = "\t10115",
            ["city"] = "Berlin\n",
            ["country"] = " AT ",
            ["email"] = " max.musterfrau@example.org  ",
        };

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body.Should().BeEquivalentTo(AddressPayloads.UpdatedRequest);
    }

    [TestCase("at")]
    [TestCase("At")]
    [TestCase("aT")]
    [TestCase(" at ")]
    public async Task PutAsync_CountryNotUppercase_ReturnsUppercaseCountry(string country)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWith("country", country));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body!.Country.Should().Be("AT");
    }

    [Test]
    public async Task PutAsync_WithoutEmail_RemovesEmail()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWithout("email"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
        (await _factory.FindAddressAsync(created.Id))!.Email.Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public async Task PutAsync_BlankEmail_RemovesEmail(string? email)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWith("email", email));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
        (await _factory.FindAddressAsync(created.Id))!.Email.Should().BeNull();
    }

    [Test]
    public async Task PutAsync_EmailOnAddressWithoutEmail_AddsEmail()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest with { Email = null });

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.Updated());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body!.Email.Should().Be(AddressPayloads.UpdatedRequest.Email);
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.Fields))]
    public async Task PutAsync_ValueAtMaxLength_Returns200WithValue(AddressPayloadField field)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await PutAsync(created.Id, AddressPayloads.ValidWith(field.Name, field.ValueAtMaxLength));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty(field.Name).GetString().Should().Be(field.ValueAtMaxLength);
    }

    [Test]
    public async Task PutAsync_SpecialCharacters_ReturnsValuesUnchanged()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        var payload = new JsonObject
        {
            ["firstName"] = "Jürgen-Đorđe",
            ["lastName"] = "Groß-Øvergård",
            ["street"] = "Rue de l'Église",
            ["houseNumber"] = "4a/1",
            ["postalCode"] = "31-000",
            ["city"] = "Kraków",
            ["country"] = "PL",
            ["email"] = "jörg@exämple.de",
        };

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (KeyValuePair<string, JsonNode?> value in payload)
        {
            body.GetProperty(value.Key).GetString().Should().Be(value.Value!.GetValue<string>());
        }
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.RequiredFields))]
    public async Task PutAsync_RequiredValueMissing_Returns400ForValue(AddressPayloadField field)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWithout(field.Name));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [TestCaseSource(nameof(BlankRequiredValues))]
    public async Task PutAsync_RequiredValueBlank_Returns400ForValue(AddressPayloadField field, string? value)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWith(field.Name, value));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.Fields))]
    public async Task PutAsync_ValueExceedsMaxLength_Returns400ForValue(AddressPayloadField field)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await PutAsync(created.Id, AddressPayloads.ValidWith(field.Name, field.ValueExceedingMaxLength));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [Test]
    public async Task PutAsync_EmptyObject_Returns400WithErrorForEachRequiredValue()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, new JsonObject());

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().BeEquivalentTo(AddressPayloads.RequiredFields.Select(field => field.Name));
    }

    [Test]
    public async Task PutAsync_AllValuesInvalid_Returns400WithErrorForEachValue()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        var payload = new JsonObject();
        foreach (AddressPayloadField field in AddressPayloads.Fields)
        {
            payload[field.Name] = field.ValueExceedingMaxLength;
        }

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, payload);

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().BeEquivalentTo(AddressPayloads.Fields.Select(field => field.Name));
        errors.EnumerateObject().Should().AllSatisfy(error => error.Value.GetArrayLength().Should().Be(1));
    }

    [TestCase("GER")]
    [TestCase("D")]
    [TestCase("de1")]
    [TestCase("XX")]
    [TestCase("1A")]
    [TestCase("D-")]
    [TestCase("ÄÖ")]
    [TestCase("UK")]
    [TestCase("D E")]
    public async Task PutAsync_InvalidCountry_Returns400ForCountry(string country)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWith("country", country));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal("country");
    }

    [TestCase("erika")]
    [TestCase("erika.example.com")]
    [TestCase("@example.com")]
    [TestCase("erika@")]
    [TestCase("erika@@example.com")]
    [TestCase("erika mustermann@example.com")]
    [TestCase("erika@example..com")]
    [TestCase("Erika <erika@example.com>")]
    [TestCase("erika@example.com, max@example.com")]
    public async Task PutAsync_InvalidEmail_Returns400ForEmail(string email)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWith("email", email));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal("email");
    }

    [Test]
    public async Task PutAsync_InvalidAddress_ReturnsValidationProblemDetails()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.ValidWith("country", "XX"));

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
    public async Task PutAsync_InvalidAddress_DoesNotChangeAddress()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        JsonObject payload = AddressPayloads.Updated();
        payload["firstName"] = " ";

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _factory.FindAddressAsync(created.Id)).Should().Be(created);
    }

    [TestCase(UnknownId)]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public async Task PutAsync_UnknownId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(id, AddressPayloads.Valid());

        // Assert
        await AssertNotFoundAsync(response);
    }

    [Test]
    public async Task PutAsync_UnknownId_ReturnsSameProblemDetailsAsUnknownRoute()
    {
        // Act
        using HttpResponseMessage response = await PutAsync(UnknownId, AddressPayloads.Valid());
        using HttpResponseMessage unknownRouteResponse =
            await _client.PutAsJsonAsync("/does-not-exist", AddressPayloads.Valid());

        // Assert
        IReadOnlyDictionary<string, string> body = await ReadWithoutTraceIdAsync(response);
        IReadOnlyDictionary<string, string> unknownRouteBody = await ReadWithoutTraceIdAsync(unknownRouteResponse);
        body.Should().Equal(unknownRouteBody);
    }

    [Test]
    public async Task PutAsync_UnknownId_DoesNotCreateAddress()
    {
        // Arrange
        int countBefore = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await PutAsync(UnknownId, AddressPayloads.Valid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _factory.FindAddressAsync(Guid.Parse(UnknownId))).Should().BeNull();
        (await _factory.ListAddressesAsync()).Should().HaveCount(countBefore);
    }

    [Test]
    public async Task PutAsync_UnknownIdAndInvalidAddress_Returns400ForValue()
    {
        // Act
        using HttpResponseMessage response = await PutAsync(UnknownId, AddressPayloads.ValidWith("country", "XX"));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal("country");
    }

    [Test]
    public async Task PutAsync_DeletedAddress_Returns404WithProblemDetails()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        using HttpResponseMessage deleteResponse = await _client.DeleteAsync($"{Path}/{created.Id}");

        // Act
        using HttpResponseMessage response = await PutAsync(created.Id, AddressPayloads.Updated());

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
    public async Task PutAsync_InvalidId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await PutAsync(id, AddressPayloads.Valid());

        // Assert
        await AssertNotFoundAsync(response);
    }

    private static IEnumerable<TestCaseData> BlankRequiredValues()
    {
        string?[] values = [null, string.Empty, " ", "   ", "\t\n"];

        return AddressPayloads.RequiredFields.SelectMany(field => values.Select(value =>
            new TestCaseData(field, value).SetArgDisplayNames(field.Name, value is null ? "null" : $"\"{value}\"")));
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

    private Task<HttpResponseMessage> PutAsync(Guid id, JsonNode payload) => PutAsync(id.ToString(), payload);

    private Task<HttpResponseMessage> PutAsync(string id, JsonNode payload) =>
        _client.PutAsJsonAsync($"{Path}/{id}", payload);
}
