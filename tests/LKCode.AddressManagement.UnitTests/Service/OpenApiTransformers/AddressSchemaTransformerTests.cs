using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class AddressSchemaTransformerTests
{
    private static readonly string[] _requestValues =
    [
        "firstName",
        "lastName",
        "street",
        "houseNumber",
        "postalCode",
        "city",
        "country",
        "email",
    ];

    private static readonly Type[] _requestTypes = [typeof(CreateAddressRequest), typeof(UpdateAddressRequest)];

    private static readonly Type[] _addressTypes = [.. _requestTypes, typeof(AddressResponse)];

    private ServiceProvider _serviceProvider = null!;
    private AddressSchemaTransformer _transformer = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _transformer = new AddressSchemaTransformer();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task TransformAsync_NullSchema_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiSchema schema = null!;

        // Act
        Func<Task> act = () =>
            _transformer.TransformAsync(schema, CreateContext(typeof(CreateAddressRequest)), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("schema");
    }

    [Test]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiSchemaTransformerContext context = null!;

        // Act
        Func<Task> act = () =>
            _transformer.TransformAsync(CreateSchema(typeof(CreateAddressRequest)), context, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [TestCaseSource(nameof(_requestTypes))]
    public async Task TransformAsync_AddressRequest_RequiresAllValuesExceptEmail(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        schema.Required.Should().BeEquivalentTo(_requestValues.Where(name => name != "email"));
    }

    [Test]
    public async Task TransformAsync_AddressResponse_RequiresAllValues()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(typeof(AddressResponse));

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(AddressResponse)), CancellationToken.None);

        // Assert
        schema.Required.Should().BeEquivalentTo([.. _requestValues, "id", "createdAt", "updatedAt"]);
    }

    [TestCaseSource(nameof(RequiredValuesOfAllTypes))]
    public async Task TransformAsync_RequiredValue_IsNonNullableStringWithLengths(
        Type type,
        string name,
        int minLength,
        int maxLength)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        IOpenApiSchema value = schema.Properties![name];
        value.Type.Should().Be(JsonSchemaType.String);
        value.MinLength.Should().Be(minLength);
        value.MaxLength.Should().Be(maxLength);
    }

    [TestCaseSource(nameof(_addressTypes))]
    public async Task TransformAsync_Country_HasPatternOfTwoUppercaseLetters(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        schema.Properties!["country"].Pattern.Should().Be("^[A-Z]{2}$");
    }

    [TestCaseSource(nameof(_addressTypes))]
    public async Task TransformAsync_Email_IsNullableEmailWithMaxLength(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        IOpenApiSchema email = schema.Properties!["email"];
        email.Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
        email.Format.Should().Be("email");
        email.MaxLength.Should().Be(AddressConstraints.EmailMaxLength);
        email.MinLength.Should().BeNull();
        email.Pattern.Should().BeNull();
    }

    [TestCaseSource(nameof(_addressTypes))]
    public async Task TransformAsync_ValuesOtherThanCountryAndEmail_HaveNoPatternAndFormat(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        _requestValues.Except(["country", "email"]).Should().AllSatisfy(name =>
        {
            schema.Properties![name].Pattern.Should().BeNull();
            schema.Properties[name].Format.Should().BeNull();
        });
    }

    [Test]
    public async Task TransformAsync_AddressResponse_KeepsIdAndTimestamps()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(typeof(AddressResponse));

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(AddressResponse)), CancellationToken.None);

        // Assert
        schema.Properties!["id"].Format.Should().Be("uuid");
        schema.Properties["id"].MaxLength.Should().BeNull();
        schema.Properties["createdAt"].Format.Should().Be("date-time");
        schema.Properties["updatedAt"].Format.Should().Be("date-time");
    }

    [Test]
    public async Task TransformAsync_SchemaWithoutProperties_OnlySetsRequired()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(CreateAddressRequest)), CancellationToken.None);

        // Assert
        schema.Properties.Should().BeNull();
        schema.Required.Should().HaveCount(7);
    }

    [TestCase(typeof(AddressListQuery))]
    [TestCase(typeof(ProblemDetails))]
    [TestCase(typeof(string))]
    [TestCase(typeof(Guid))]
    public async Task TransformAsync_OtherType_KeepsSchemaUnchanged(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);
        string before = await SerializeAsync(schema);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        (await SerializeAsync(schema)).Should().Be(before);
    }

    private static IEnumerable<TestCaseData> RequiredValuesOfAllTypes()
    {
        (string Name, int MinLength, int MaxLength)[] values =
        [
            ("firstName", 1, AddressConstraints.FirstNameMaxLength),
            ("lastName", 1, AddressConstraints.LastNameMaxLength),
            ("street", 1, AddressConstraints.StreetMaxLength),
            ("houseNumber", 1, AddressConstraints.HouseNumberMaxLength),
            ("postalCode", 1, AddressConstraints.PostalCodeMaxLength),
            ("city", 1, AddressConstraints.CityMaxLength),
            ("country", 2, AddressConstraints.CountryMaxLength),
        ];

        return _addressTypes.SelectMany(type => values.Select(value =>
            new TestCaseData(type, value.Name, value.MinLength, value.MaxLength)
                .SetArgDisplayNames(type.Name, value.Name)));
    }

    /// <summary>
    /// Creates a schema like the OpenAPI generator does: an object with a property for each JSON property, all
    /// required, without constraints.
    /// </summary>
    private static OpenApiSchema CreateSchema(Type type)
    {
        JsonTypeInfo typeInfo = JsonSerializerOptions.Web.GetTypeInfo(type);
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return new OpenApiSchema { Type = JsonSchemaType.String };
        }

        var properties = new Dictionary<string, IOpenApiSchema>();
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            properties[property.Name] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Format = property.PropertyType == typeof(Guid) ? "uuid"
                    : property.PropertyType == typeof(DateTimeOffset) ? "date-time"
                    : null,
            };
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = properties,
            Required = new HashSet<string>(properties.Keys),
        };
    }

    private static async Task<string> SerializeAsync(OpenApiSchema schema)
    {
        await using var writer = new StringWriter();
        schema.SerializeAsV31(new OpenApiJsonWriter(writer));

        return writer.ToString();
    }

    private OpenApiSchemaTransformerContext CreateContext(Type type) => new()
    {
        DocumentName = "v1",
        JsonTypeInfo = JsonSerializerOptions.Web.GetTypeInfo(type),
        JsonPropertyInfo = null,
        ParameterDescription = null,
        ApplicationServices = _serviceProvider,
    };
}
