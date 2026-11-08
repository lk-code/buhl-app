using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Domain;
using Microsoft.Extensions.DependencyInjection.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services
    .AddDomain()
    .AddCoreAddresses()
    .AddData(builder.Configuration);

// System.Text.Json is the serializer of the controllers; Newtonsoft.Json is not used.
builder.Services.AddControllers();

WebApplication app = builder.Build();

app.MapControllers();

app.Run();
