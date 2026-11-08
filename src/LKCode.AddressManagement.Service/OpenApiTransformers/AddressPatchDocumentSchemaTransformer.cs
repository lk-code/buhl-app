using System.Text.Json.Nodes;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.Extensions;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the JSON Patch documents (RFC 6902) that change an address in the OpenAPI schema of
/// <see cref="JsonPatchDocument{TModel}"/> of <see cref="UpdateAddressRequest"/>.
/// </summary>
/// <remarks>
/// ASP.NET Core describes every JSON Patch document with the same generic schema: an array of operations, one of
/// <c>add</c>/<c>replace</c>/<c>test</c>, <c>move</c>/<c>copy</c> and <c>remove</c>, whose <c>path</c> and
/// <c>from</c> are any string and whose <c>value</c> is any value. The API accepts only the JSON Pointers of the
/// fields of an address, which are all strings, so these constraints are added. The pointers come from the JSON
/// options, like the ones the API checks the operations with.
/// </remarks>
public sealed class AddressPatchDocumentSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Restricts <c>path</c> and <c>from</c> of all operations to the fields of an address and <c>value</c> to a
    /// string or <see langword="null"/>, if the <paramref name="schema"/> describes a patch document of an address;
    /// other schemas stay unchanged.
    /// </summary>
    /// <param name="schema">The schema to change.</param>
    /// <param name="context">The context of the schema with the type it describes.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
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

        if (context.JsonTypeInfo.Type != typeof(JsonPatchDocument<UpdateAddressRequest>)
            || schema.Items is not OpenApiSchema { OneOf: { } operations } items)
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<string> fieldPointers =
            context.JsonTypeInfo.Options.GetPropertyJsonPointers(typeof(UpdateAddressRequest));

        schema.Description =
            "A JSON Patch document (RFC 6902): the operations to apply to the values of an address, in their order.";
        items.Description = "An operation on a field of the address.";
        foreach (IOpenApiSchema operation in operations)
        {
            if (GetMember(operation, "op") is { } op)
            {
                op.Description = "The operation to apply.";
            }

            if (GetMember(operation, "path") is { } path)
            {
                path.Enum = CreateEnum(fieldPointers);
                path.Description = "The field of the address as JSON Pointer (RFC 6901), e.g. `/firstName`.";
            }

            if (GetMember(operation, "from") is { } from)
            {
                from.Enum = CreateEnum(fieldPointers);
                from.Description = "The field of the address to move or copy the value from, as JSON Pointer.";
            }

            if (GetMember(operation, "value") is { } value)
            {
                value.Type = JsonSchemaType.String | JsonSchemaType.Null;
                value.Description =
                    "The new value of the field (`add`, `replace`) or the expected value (`test`). The changed "
                    + "address must meet the constraints of `UpdateAddressRequest`.";
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema? GetMember(IOpenApiSchema operation, string name) =>
        operation.Properties?.TryGetValue(name, out IOpenApiSchema? member) == true
            ? member as OpenApiSchema
            : null;

    private static List<JsonNode> CreateEnum(IReadOnlyList<string> values) =>
        [.. values.Select(value => (JsonNode)JsonValue.Create(value))];
}
