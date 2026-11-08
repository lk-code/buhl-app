using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Domain;
using LKCode.AddressManagement.Service.ExceptionHandlers;
using Microsoft.Extensions.DependencyInjection.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services
    .AddDomain()
    .AddCoreAddresses()
    .AddData(builder.Configuration);

// System.Text.Json is the serializer of the controllers; Newtonsoft.Json is not used.
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

WebApplication app = builder.Build();

// Handles all exceptions before the developer exception page, so no details are exposed in any environment.
app.UseExceptionHandler();

// Writes Problem Details for error responses without a body, e.g. 404 for an unknown route.
app.UseStatusCodePages();

app.MapControllers();

app.Run();
