using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace LKCode.AddressManagement.IntegrationTests.Fakes;

/// <summary>
/// Throws an unexpected exception. It is only part of the hosts that
/// <see cref="WebApplicationFactoryExtensions.WithThrowingController"/> creates.
/// </summary>
[ApiController]
[Route(Path)]
public sealed class ThrowingController : ControllerBase
{
    public const string Path = "test/throw";
    public const string ExceptionMessage = "Sensitive internal detail";

    [HttpGet]
    [SuppressMessage("Performance", "CA1822", Justification = "MVC only discovers instance methods as actions.")]
    public IActionResult Get()
    {
        throw new InvalidOperationException(ExceptionMessage);
    }
}
