using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Queries;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the numbers of the OpenAPI schema of <see cref="AddressListResponse"/> as the API writes them.
/// </summary>
/// <remarks>
/// The generated schema describes the numbers as integer or string, because the JSON options allow reading numbers
/// from strings; the API always writes integers. The ranges come from <see cref="AddressListConstraints"/>.
/// </remarks>
public sealed class AddressListResponseSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// The ranges of the numbers, by the name of the property in <see cref="AddressListResponse"/>.
    /// </summary>
    private static readonly FrozenDictionary<string, IntegerRange> _ranges =
        new Dictionary<string, IntegerRange>(StringComparer.Ordinal)
        {
            [nameof(AddressListResponse.Page)] = new(AddressListConstraints.MinPage, Maximum: null),
            [nameof(AddressListResponse.PageSize)] =
                new(AddressListConstraints.MinPageSize, AddressListConstraints.MaxPageSize),
            [nameof(AddressListResponse.TotalCount)] = new(0, Maximum: null),
            [nameof(AddressListResponse.TotalPages)] = new(0, Maximum: null),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Describes the page, the page size and the counts as 32-bit integers with their ranges and requires all
    /// members, if the <paramref name="schema"/> describes <see cref="AddressListResponse"/>; other schemas stay
    /// unchanged.
    /// </summary>
    /// <param name="schema">The schema to change.</param>
    /// <param name="context">The context of the schema with the type it describes.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>The schemas of the numbers are changed in place, so that their descriptions are kept.</remarks>
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

        if (context.JsonTypeInfo.Type != typeof(AddressListResponse))
        {
            return Task.CompletedTask;
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonPropertyInfo property in context.JsonTypeInfo.Properties)
        {
            required.Add(property.Name);

            string? memberName = (property.AttributeProvider as MemberInfo)?.Name;
            if (memberName is not null
                && _ranges.TryGetValue(memberName, out IntegerRange? range)
                && schema.Properties?.TryGetValue(property.Name, out IOpenApiSchema? value) == true
                && value is OpenApiSchema numberSchema)
            {
                range.ApplyTo(numberSchema);
            }
        }

        schema.Required = required;

        return Task.CompletedTask;
    }

    /// <summary>
    /// The range of a 32-bit integer.
    /// </summary>
    /// <param name="Minimum">The smallest value.</param>
    /// <param name="Maximum">The largest value, or <see langword="null"/> for the largest 32-bit integer.</param>
    private sealed record IntegerRange(int Minimum, int? Maximum)
    {
        public void ApplyTo(OpenApiSchema schema)
        {
            schema.Type = JsonSchemaType.Integer;
            schema.Format = "int32";
            schema.Pattern = null;
            schema.Minimum = Minimum.ToString(CultureInfo.InvariantCulture);
            schema.Maximum = Maximum?.ToString(CultureInfo.InvariantCulture);
        }
    }
}
