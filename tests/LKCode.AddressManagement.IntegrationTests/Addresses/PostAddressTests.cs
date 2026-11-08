using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

public class PostAddressTests
{
    private const string Path = "/api/addresses";
    private const string ProblemDetailsMediaType = "application/problem+json";

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
    public async Task PostAsync_ValidAddress_Returns201WithAddress()
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.Valid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body.Should().BeEquivalentTo(AddressPayloads.ValidRequest);
        body.Id.Version.Should().Be(7);
        body.CreatedAt.Should().BeCloseTo(TimeProvider.System.GetUtcNow(), TimeSpan.FromMinutes(1));
        body.UpdatedAt.Should().Be(body.CreatedAt);
    }

    [Test]
    public async Task PostAsync_ValidAddress_ReturnsAbsoluteLocationOfAddress()
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.Valid());

        // Assert
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location.IsAbsoluteUri.Should().BeTrue();
        response.Headers.Location.Should().Be(new Uri(_client.BaseAddress!, $"{Path}/{body!.Id}"));
    }

    [Test]
    public async Task PostAsync_ValidAddress_AddressCanBeReadFromLocation()
    {
        // Arrange
        using HttpResponseMessage response = await PostAsync(AddressPayloads.Valid());
        AddressResponse? created = await response.Content.ReadFromJsonAsync<AddressResponse>();

        // Act
        using HttpResponseMessage getResponse = await _client.GetAsync(response.Headers.Location);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse? read = await getResponse.Content.ReadFromJsonAsync<AddressResponse>();
        read.Should().Be(created);
    }

    [Test]
    public async Task PostAsync_ValidAddress_StoresAddress()
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.Valid());

        // Assert
        AddressResponse? created = await response.Content.ReadFromJsonAsync<AddressResponse>();
        IReadOnlyList<AddressResponse> stored = await _factory.ListAddressesAsync();
        stored.Should().ContainEquivalentOf(created);
    }

    [Test]
    public async Task PostAsync_SameAddressTwice_CreatesTwoAddresses()
    {
        // Act
        using HttpResponseMessage first = await PostAsync(AddressPayloads.Valid());
        using HttpResponseMessage second = await PostAsync(AddressPayloads.Valid());

        // Assert
        AddressResponse? firstBody = await first.Content.ReadFromJsonAsync<AddressResponse>();
        AddressResponse? secondBody = await second.Content.ReadFromJsonAsync<AddressResponse>();
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        secondBody!.Id.Should().NotBe(firstBody!.Id);
        second.Headers.Location.Should().NotBe(first.Headers.Location);
    }

    [Test]
    public async Task PostAsync_ValuesWithSurroundingWhitespace_ReturnsTrimmedValues()
    {
        // Arrange
        var payload = new JsonObject
        {
            ["firstName"] = "  Erika ",
            ["lastName"] = " Mustermann",
            ["street"] = "Heidestraße  ",
            ["houseNumber"] = " 17 ",
            ["postalCode"] = "\t51147",
            ["city"] = "Köln\n",
            ["country"] = " DE ",
            ["email"] = " erika.mustermann@example.com  ",
        };

        // Act
        using HttpResponseMessage response = await PostAsync(payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body.Should().BeEquivalentTo(AddressPayloads.ValidRequest);
    }

    [TestCase("de")]
    [TestCase("De")]
    [TestCase("dE")]
    [TestCase(" de ")]
    public async Task PostAsync_CountryNotUppercase_ReturnsUppercaseCountry(string country)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWith("country", country));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body!.Country.Should().Be("DE");
    }

    [Test]
    public async Task PostAsync_WithoutEmail_Returns201WithoutEmail()
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWithout("email"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public async Task PostAsync_BlankEmail_Returns201WithoutEmail(string? email)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWith("email", email));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.Fields))]
    public async Task PostAsync_ValueAtMaxLength_Returns201WithValue(AddressPayloadField field)
    {
        // Act
        using HttpResponseMessage response =
            await PostAsync(AddressPayloads.ValidWith(field.Name, field.ValueAtMaxLength));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty(field.Name).GetString().Should().Be(field.ValueAtMaxLength);
    }

    [Test]
    public async Task PostAsync_SpecialCharacters_ReturnsValuesUnchanged()
    {
        // Arrange
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
        using HttpResponseMessage response = await PostAsync(payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (KeyValuePair<string, JsonNode?> value in payload)
        {
            body.GetProperty(value.Key).GetString().Should().Be(value.Value!.GetValue<string>());
        }
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.RequiredFields))]
    public async Task PostAsync_RequiredValueMissing_Returns400ForValue(AddressPayloadField field)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWithout(field.Name));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [TestCaseSource(nameof(BlankRequiredValues))]
    public async Task PostAsync_RequiredValueBlank_Returns400ForValue(AddressPayloadField field, string? value)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWith(field.Name, value));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [TestCaseSource(typeof(AddressPayloads), nameof(AddressPayloads.Fields))]
    public async Task PostAsync_ValueExceedsMaxLength_Returns400ForValue(AddressPayloadField field)
    {
        // Act
        using HttpResponseMessage response =
            await PostAsync(AddressPayloads.ValidWith(field.Name, field.ValueExceedingMaxLength));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal(field.Name);
    }

    [Test]
    public async Task PostAsync_EmptyObject_Returns400WithErrorForEachRequiredValue()
    {
        // Act
        using HttpResponseMessage response = await PostAsync(new JsonObject());

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().BeEquivalentTo(AddressPayloads.RequiredFields.Select(field => field.Name));
    }

    [Test]
    public async Task PostAsync_AllValuesInvalid_Returns400WithErrorForEachValue()
    {
        // Arrange
        var payload = new JsonObject();
        foreach (AddressPayloadField field in AddressPayloads.Fields)
        {
            payload[field.Name] = field.ValueExceedingMaxLength;
        }

        // Act
        using HttpResponseMessage response = await PostAsync(payload);

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
    public async Task PostAsync_InvalidCountry_Returns400ForCountry(string country)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWith("country", country));

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
    public async Task PostAsync_InvalidEmail_Returns400ForEmail(string email)
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWith("email", email));

        // Assert
        JsonElement errors = await ReadValidationErrorsAsync(response);
        GetKeys(errors).Should().Equal("email");
    }

    [Test]
    public async Task PostAsync_InvalidAddress_ReturnsValidationProblemDetails()
    {
        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWith("country", "XX"));

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
    public async Task PostAsync_InvalidAddress_DoesNotStoreAddress()
    {
        // Arrange
        int countBefore = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await PostAsync(AddressPayloads.ValidWith("firstName", " "));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _factory.ListAddressesAsync()).Should().HaveCount(countBefore);
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

    private Task<HttpResponseMessage> PostAsync(JsonNode payload) => _client.PostAsJsonAsync(Path, payload);
}
