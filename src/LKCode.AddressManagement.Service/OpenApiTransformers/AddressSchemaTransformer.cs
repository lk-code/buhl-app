using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the constraints of the values of an address in the OpenAPI schemas of the address requests and of
/// <see cref="AddressResponse"/>.
/// </summary>
/// <remarks>
/// The maximum lengths come from <see cref="AddressConstraints"/>, the single source of the validation, the database
/// mapping and the OpenAPI document. The schemas describe sanitized values: the API also accepts values with
/// surrounding whitespace or a lowercase country, because it sanitizes them before the validation.
/// </remarks>
public sealed class AddressSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Adds the constraints to the <paramref name="schema"/> if it describes a type that implements
    /// <see cref="IAddressRequest"/> or <see cref="AddressResponse"/>; other schemas stay unchanged.
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
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
