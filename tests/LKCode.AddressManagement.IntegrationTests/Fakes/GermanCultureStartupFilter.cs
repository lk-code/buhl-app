using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace LKCode.AddressManagement.IntegrationTests.Fakes;

/// <summary>
/// Processes every request with the German culture, like a server whose operating system is set to German.
/// </summary>
public sealed class GermanCultureStartupFilter : IStartupFilter
{
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de-DE");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            CultureInfo.CurrentCulture = _german;
            CultureInfo.CurrentUICulture = _german;
            await nextMiddleware(context);
        });
        next(app);
    };
}
