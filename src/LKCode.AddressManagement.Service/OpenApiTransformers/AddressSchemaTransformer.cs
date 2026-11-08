using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Validators;
using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the constraints of the values of an address in the OpenAPI schemas of the address requests and of
/// <see cref="AddressResponse"/>, including its version.
/// </summary>
/// <remarks>
/// The maximum lengths come from <see cref="AddressConstraints"/>, the single source of the validation, the database
/// mapping and the OpenAPI document. The schemas describe sanitized values: the API also accepts values with
/// surrounding whitespace or a lowercase country, because it sanitizes them before the validation.
/// </remarks>
public sealed class AddressSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// The constraints of the values, by the name of the property in <see cref="IAddressRequest"/> and
    /// <see cref="AddressResponse"/>.
    /// </summary>
    private static readonly FrozenDictionary<string, ValueConstraints> _constraints =
        new Dictionary<string, ValueConstraints>(StringComparer.Ordinal)
        {
            [nameof(IAddressRequest.FirstName)] = ValueConstraints.Required(AddressConstraints.FirstNameMaxLength),
            [nameof(IAddressRequest.LastName)] = ValueConstraints.Required(AddressConstraints.LastNameMaxLength),
            [nameof(IAddressRequest.Street)] = ValueConstraints.Required(AddressConstraints.StreetMaxLength),
            [nameof(IAddressRequest.HouseNumber)] =
                ValueConstraints.Required(AddressConstraints.HouseNumberMaxLength),
            [nameof(IAddressRequest.PostalCode)] = ValueConstraints.Required(AddressConstraints.PostalCodeMaxLength),
            [nameof(IAddressRequest.City)] = ValueConstraints.Required(AddressConstraints.CityMaxLength),

            // A country code has exactly the maximum length.
            [nameof(IAddressRequest.Country)] = ValueConstraints.Required(AddressConstraints.CountryMaxLength) with
            {
                MinLength = AddressConstraints.CountryMaxLength,
                Pattern = CountryCodes.Pattern,
            },
            [nameof(IAddressRequest.Email)] = new(
                IsOptional: true,
                MinLength: null,
                AddressConstraints.EmailMaxLength,
                Pattern: null,
                Format: "email"),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Adds the constraints to the <paramref name="schema"/> if it describes a type that implements
    /// <see cref="IAddressRequest"/> or <see cref="AddressResponse"/>; other schemas stay unchanged.
    /// </summary>
    /// <param name="schema">The schema to change.</param>
    /// <param name="context">The context of the schema with the type it describes.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// In a request, all values except the email address are required. A response always contains all values; the
    /// email address is <see langword="null"/> if none is set.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="schema"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        Type type = context.JsonTypeInfo.Type;
        if (typeof(IAddressRequest).IsAssignableFrom(type))
        {
            ApplyConstraints(schema, context.JsonTypeInfo, requireAll: false);
        }
        else if (type == typeof(AddressResponse))
        {
            ApplyConstraints(schema, context.JsonTypeInfo, requireAll: true);
            DescribeVersion(schema, context.JsonTypeInfo);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Describes <see cref="AddressResponse.Version"/> as the 64-bit integer the API writes, starting at 1. The
    /// generated schema also allows a string, because the JSON options allow reading numbers from strings.
    /// </summary>
    private static void DescribeVersion(OpenApiSchema schema, JsonTypeInfo typeInfo)
    {
        JsonPropertyInfo? property = typeInfo.Properties.FirstOrDefault(property =>
            (property.AttributeProvider as MemberInfo)?.Name == nameof(AddressResponse.Version));
        if (property is not null
            && schema.Properties?.TryGetValue(property.Name, out IOpenApiSchema? value) == true
            && value is OpenApiSchema versionSchema)
        {
            versionSchema.Type = JsonSchemaType.Integer;
            versionSchema.Format = "int64";
            versionSchema.Minimum = "1";
            versionSchema.Pattern = null;
        }
    }

    private static void ApplyConstraints(OpenApiSchema schema, JsonTypeInfo typeInfo, bool requireAll)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            string? memberName = (property.AttributeProvider as MemberInfo)?.Name;
            ValueConstraints? constraints = memberName is null ? null : _constraints.GetValueOrDefault(memberName);
            if (requireAll || constraints is { IsOptional: false })
            {
                required.Add(property.Name);
            }

            if (constraints is not null
                && schema.Properties?.TryGetValue(property.Name, out IOpenApiSchema? value) == true
                && value is OpenApiSchema propertySchema)
            {
                constraints.ApplyTo(propertySchema);
            }
        }

        schema.Required = required;
    }

    /// <summary>
    /// The constraints of a text value of an address.
    /// </summary>
    /// <param name="IsOptional">
    /// Whether the value may be missing in a request and <see langword="null"/> in a response.
    /// </param>
    /// <param name="MinLength">The minimum length, or <see langword="null"/> if there is none.</param>
    /// <param name="MaxLength">The maximum length.</param>
    /// <param name="Pattern">The regular expression the value must match, or <see langword="null"/>.</param>
    /// <param name="Format">The format of the value, e.g. <c>email</c>, or <see langword="null"/>.</param>
    private sealed record ValueConstraints(
        bool IsOptional,
        int? MinLength,
        int MaxLength,
        string? Pattern,
        string? Format)
    {
        /// <summary>
        /// Creates the constraints of a required value, which must not be empty.
        /// </summary>
        public static ValueConstraints Required(int maxLength) => new(
            IsOptional: false,
            MinLength: 1,
            maxLength,
            Pattern: null,
            Format: null);

        public void ApplyTo(OpenApiSchema schema)
        {
            schema.Type = IsOptional ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
            schema.MinLength = MinLength;
            schema.MaxLength = MaxLength;
            schema.Pattern = Pattern;
            schema.Format = Format;
        }
    }
}
