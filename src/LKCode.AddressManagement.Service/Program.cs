using System.Globalization;
using FluentValidation;
using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Domain;
using LKCode.AddressManagement.Service;
using LKCode.AddressManagement.Service.ExceptionHandlers;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

const string DevelopmentOutputTemplate =
    "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Serilog writes to the console only: readable text in development, compact JSON everywhere else. The levels come
// from the section "Serilog" of the configuration. The static logger stays untouched, so several hosts in one
// process (e.g. in tests) do not share a logger.
builder.Services.AddSerilog(
    (services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();

        if (builder.Environment.IsDevelopment())
        {
            loggerConfiguration.WriteTo.Console(
                outputTemplate: DevelopmentOutputTemplate,
                formatProvider: CultureInfo.InvariantCulture);
        }
        else
        {
            loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
        }
    },
    preserveStaticLogger: true);

// The validation messages are part of the Problem Details and therefore always English. FluentValidation would
// otherwise translate its default messages into the culture of the server.
ValidatorOptions.Global.LanguageManager.Enabled = false;

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services
    .AddDomain()
    .AddCoreAddresses()
    .AddData(builder.Configuration)
    .AddAddressCaching(builder.Configuration);

// System.Text.Json is the serializer of the controllers; Newtonsoft.Json is not used. MVC does not treat
// non-nullable properties as implicitly required: requests are sanitized and then validated completely by
// FluentValidation, so that all field errors are reported together and blank values are trimmed first. Errors in
// the JSON of a request only report the path with a generic message; the messages of System.Text.Json would expose
// internal type names.
builder.Services
    .AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .ConfigureApiBehaviorOptions(options =>
    {
        // The actions check the model state themselves: a change evaluates its preconditions (If-Match) before it
        // processes the body (RFC 9110, section 13.2.1), so an invalid body must not end the request beforehand.
        options.SuppressModelStateInvalidFilter = true;

        // MVC has no defaults for 428 (RFC 6585, section 3); its Problem Details would lack type and title.
        options.ClientErrorMapping[StatusCodes.Status428PreconditionRequired] = new ClientErrorData
        {
            Link = "https://tools.ietf.org/html/rfc6585#section-3",
            Title = "Precondition Required",
        };
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<AddressSchemaTransformer>();
    options.AddSchemaTransformer<AddressListResponseSchemaTransformer>();
    options.AddSchemaTransformer<AddressPatchDocumentSchemaTransformer>();
    options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>();
    options.AddOperationTransformer<LocationHeaderOperationTransformer>();
    options.AddOperationTransformer<ETagHeaderOperationTransformer>();
    options.AddOperationTransformer<IfMatchHeaderOperationTransformer>();
    options.AddOperationTransformer<AddressListParametersOperationTransformer>();
});

WebApplication app = builder.Build();

// Outermost, so that it logs the final status code of every request, including 500 and 499. The logger is passed
// explicitly, because the middleware would otherwise write to the untouched static logger.
app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());

// Handles all exceptions before the developer exception page, so no details are exposed in any environment.
app.UseExceptionHandler();

// Writes Problem Details for error responses without a body, e.g. 404 for an unknown route.
app.UseStatusCodePages();

// The OpenAPI document is the contract of the API and is therefore available in every environment.
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
