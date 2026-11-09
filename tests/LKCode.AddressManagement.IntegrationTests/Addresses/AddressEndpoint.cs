using System.Net;
using System.Net.Http.Json;
using LKCode.AddressManagement.Service.AuthorizationPolicies;
using Microsoft.Net.Http.Headers;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// One of the six address endpoints with the scope it requires and a valid request, so that a request to it can only
/// fail because of its authorization.
/// </summary>
public sealed record AddressEndpoint(HttpMethod Method, bool HasId, string Scope, HttpStatusCode SuccessStatusCode)
{
    private const string Path = AddressChangeRequests.Path;

    /// <summary>
    /// Gets all address endpoints.
    /// </summary>
    public static IReadOnlyList<AddressEndpoint> All { get; } =
    [
        new(HttpMethod.Get, false, AddressScopes.Read, HttpStatusCode.OK),
        new(HttpMethod.Get, true, AddressScopes.Read, HttpStatusCode.OK),
        new(HttpMethod.Post, false, AddressScopes.Write, HttpStatusCode.Created),
        new(HttpMethod.Put, true, AddressScopes.Write, HttpStatusCode.OK),
        new(HttpMethod.Patch, true, AddressScopes.Write, HttpStatusCode.OK),
        new(HttpMethod.Delete, true, AddressScopes.Write, HttpStatusCode.NoContent),
    ];

    /// <summary>
    /// Gets the endpoints that read addresses and require <see cref="AddressScopes.Read"/>.
    /// </summary>
    public static IReadOnlyList<AddressEndpoint> Reading { get; } =
        [.. All.Where(endpoint => endpoint.Scope == AddressScopes.Read)];

    /// <summary>
    /// Gets the endpoints that change addresses and require <see cref="AddressScopes.Write"/>.
    /// </summary>
    public static IReadOnlyList<AddressEndpoint> Changing { get; } =
        [.. All.Where(endpoint => endpoint.Scope == AddressScopes.Write)];

    /// <summary>
    /// Gets the endpoints of a single address.
    /// </summary>
    public static IReadOnlyList<AddressEndpoint> WithId { get; } = [.. All.Where(endpoint => endpoint.HasId)];

    /// <summary>
    /// Creates a valid request for the address with the <paramref name="id"/>: a change has a valid body and the
    /// header <c>If-Match: *</c>.
    /// </summary>
    public HttpRequestMessage CreateRequest(Guid id)
    {
        var request = new HttpRequestMessage(Method, HasId ? $"{Path}/{id}" : Path);
        if (Method == HttpMethod.Post)
        {
            request.Content = JsonContent.Create(AddressPayloads.Valid());
        }
        else if (HasId && Scope == AddressScopes.Write)
        {
            request.Content = AddressChangeRequests.CreateValidContent(Method);
            request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, "*");
        }

        return request;
    }

    public override string ToString() => $"{Method} {Path}{(HasId ? "/{id}" : string.Empty)}";
}
