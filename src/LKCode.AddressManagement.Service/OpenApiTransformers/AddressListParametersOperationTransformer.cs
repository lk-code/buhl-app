using System.Globalization;
using System.Text.Json.Nodes;
using LKCode.AddressManagement.Core.Addresses.Queries;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the paging and sorting query parameters of the address list in the OpenAPI document: their types,
/// ranges, allowed values and defaults.
/// </summary>
/// <remarks>
/// The values come from <see cref="AddressListConstraints"/>, <see cref="AddressSortFields"/> and
/// <see cref="SortDirections"/>, the sources of the validation. Without this transformer, the numbers would also be
/// described as strings, because the JSON options allow reading numbers from strings, and the sort parameters would
/// be arbitrary strings. The names of the sort field and the sort direction are described as the API returns them;
/// the API also accepts them in any case and with surrounding whitespace, because it sanitizes them first.
/// </remarks>
public sealed class AddressListParametersOperationTransformer : IOpenApiOperationTransformer
{
    private const string PageParameter = "page";
    private const string PageSizeParameter = "pageSize";
    private const string SortByParameter = "sortBy";
    private const string SortDirectionParameter = "sortDirection";
    private const string Int32Format = "int32";

    /// <summary>
    /// Replaces the schemas of the query parameters <c>page</c>, <c>pageSize</c>, <c>sortBy</c> and
    /// <c>sortDirection</c> of the <paramref name="operation"/>; other parameters stay unchanged.
    /// </summary>
    /// <param name="operation">The operation to change.</param>
    /// <param name="context">The context of the operation.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        foreach (IOpenApiParameter parameter in operation.Parameters ?? [])
        {
            if (parameter is OpenApiParameter { In: ParameterLocation.Query } queryParameter
                && CreateSchema(queryParameter.Name) is { } schema)
            {
                queryParameter.Schema = schema;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new schema for the query parameter with the <paramref name="name"/>, so that operations do not share
    /// it; <see langword="null"/> for any other parameter.
    /// </summary>
    private static OpenApiSchema? CreateSchema(string? name) => name switch
    {
        PageParameter => new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Format = Int32Format,
            Minimum = ToText(AddressListConstraints.MinPage),
            Maximum = ToText(int.MaxValue),
            Default = JsonValue.Create(AddressListConstraints.DefaultPage),
        },
        PageSizeParameter => new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Format = Int32Format,
            Minimum = ToText(AddressListConstraints.MinPageSize),
            Maximum = ToText(AddressListConstraints.MaxPageSize),
            Default = JsonValue.Create(AddressListConstraints.DefaultPageSize),
        },
        SortByParameter => CreateEnumSchema(
            AddressSortFields.Names,
            AddressSortFields.GetName(AddressListConstraints.DefaultSortBy)),
        SortDirectionParameter => CreateEnumSchema(
            SortDirections.Names,
            SortDirections.GetName(AddressListConstraints.DefaultSortDirection)),
        _ => null,
    };

    private static OpenApiSchema CreateEnumSchema(IEnumerable<string> names, string defaultName) => new()
    {
        Type = JsonSchemaType.String,
        Enum = [.. names.Select(JsonNode (name) => JsonValue.Create(name))],
        Default = JsonValue.Create(defaultName),
    };

    private static string ToText(int value) => value.ToString(CultureInfo.InvariantCulture);
}
