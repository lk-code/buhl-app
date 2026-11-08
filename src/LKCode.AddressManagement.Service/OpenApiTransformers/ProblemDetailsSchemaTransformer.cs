using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the Problem Details (RFC 9457) in the OpenAPI schemas of <see cref="ProblemDetails"/> and the types
/// derived from it, such as <see cref="ValidationProblemDetails"/>, as this API writes them.
/// </summary>
/// <remarks>
/// Every Problem Details response of the API contains the members <c>type</c>, <c>title</c>, <c>status</c> and
/// <c>traceId</c>, a response with validation errors additionally <c>errors</c>. The generated schema does not know
/// the extension member <c>traceId</c> and describes <c>status</c> as integer or string, because the JSON options
/// allow reading numbers from strings; the API always writes a number.
/// </remarks>
public sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string UriReferenceFormat = "uri-reference";
    private const string ErrorsMember = "errors";

    private static readonly string[] _requiredMembers = ["type", "title", "status", "traceId"];

    /// <summary>
    /// Describes the members of the Problem Details in the <paramref name="schema"/> if it describes a type that is
    /// assignable to <see cref="ProblemDetails"/>; other schemas stay unchanged.
    /// </summary>
    /// <param name="schema">The schema to change.</param>
    /// <param name="context">The context of the schema with the type it describes.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// The members defined by RFC 9457 and <c>traceId</c> are replaced or added; other members, such as the
    /// <c>errors</c> of a validation, keep their schema and only get a description.
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

        if (!typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        schema.Properties["type"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = UriReferenceFormat,
            Description = "A URI reference that identifies the problem type.",
        };
        schema.Properties["title"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "A short, human-readable summary of the problem type.",
        };
        schema.Properties["status"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Format = "int32",
            Minimum = "100",
            Maximum = "599",
            Description = "The HTTP status code of the response.",
        };
        schema.Properties["detail"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "A human-readable explanation specific to this occurrence of the problem.",
        };
        schema.Properties["instance"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Format = UriReferenceFormat,
            Description = "A URI reference that identifies the specific occurrence of the problem.",
        };
        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "The identifier of the request, which links the response to the log entries.",
        };

        var required = new HashSet<string>(_requiredMembers, StringComparer.Ordinal);
        if (schema.Properties.TryGetValue(ErrorsMember, out IOpenApiSchema? errors))
        {
            required.Add(ErrorsMember);
            if (errors is OpenApiSchema errorsSchema)
            {
                errorsSchema.Description =
                    "The validation errors: the messages by the name of the field or by the JSON path of the value.";
            }
        }

        schema.Required = required;

        return Task.CompletedTask;
    }
}
