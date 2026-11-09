using LKCode.AddressManagement.Service.AuthorizationPolicies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes for every protected operation in the OpenAPI document that it requires a bearer token with the scopes of
/// its authorization policy, and the <c>WWW-Authenticate</c> header of its <c>401 Unauthorized</c> response.
/// </summary>
/// <remarks>
/// The policy of an endpoint is determined like the authorization middleware does it: from the authorization metadata
/// of the endpoint, or the fallback policy if it has none; an endpoint that allows anonymous access is not protected.
/// The scopes are the scopes of the <see cref="ScopeRequirement"/>s of the policy. OpenAPI 3.1 allows them in the
/// security requirement of a scheme of the type <c>http</c>, too. The scheme itself is described by the
/// <see cref="BearerSecuritySchemeDocumentTransformer"/>.
/// </remarks>
/// <param name="policyProvider">The provider of the authorization policies of the endpoints.</param>
/// <exception cref="ArgumentNullException"><paramref name="policyProvider"/> is <see langword="null"/>.</exception>
public sealed class SecurityRequirementOperationTransformer(IAuthorizationPolicyProvider policyProvider)
    : IOpenApiOperationTransformer
{
    private const string UnauthorizedStatusCode = "401";

    private const string WwwAuthenticateDescription =
        "The challenge of the scheme `Bearer` (RFC 6750, section 3): only `Bearer` if the request has no bearer "
        + "token; for an invalid token additionally the error `invalid_token` and its description, e.g. "
        + "`Bearer error=\"invalid_token\", error_description=\"The token expired at '...'\"`.";

    private readonly IAuthorizationPolicyProvider _policyProvider =
        policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));

    /// <summary>
    /// Adds the security requirement of the bearer scheme with the scopes of the policy to the
    /// <paramref name="operation"/> and the header <c>WWW-Authenticate</c> to its <c>401 Unauthorized</c> response,
    /// if its endpoint is protected.
    /// </summary>
    /// <param name="operation">The operation to change.</param>
    /// <param name="context">The context of the operation with the metadata of its endpoint and the document.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A task that completes when the operation is changed.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        AuthorizationPolicy? policy = await AuthorizationPolicy.CombineAsync(
            _policyProvider,
            [.. metadata.OfType<IAuthorizeData>()],
            [.. metadata.OfType<AuthorizationPolicy>()]);
        if (policy is null)
        {
            return;
        }

        List<string> scopes =
        [
            .. policy.Requirements.OfType<ScopeRequirement>()
                .Select(requirement => requirement.Scope)
                .Distinct(StringComparer.Ordinal),
        ];
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document)] = scopes,
        });

        if (operation.Responses?.GetValueOrDefault(UnauthorizedStatusCode) is OpenApiResponse response)
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[HeaderNames.WWWAuthenticate] = new OpenApiHeader
            {
                Required = true,
                Description = WwwAuthenticateDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            };
        }
    }
}
