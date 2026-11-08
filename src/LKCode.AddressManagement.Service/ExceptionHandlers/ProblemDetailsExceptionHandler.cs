using Microsoft.AspNetCore.Diagnostics;

namespace LKCode.AddressManagement.Service.ExceptionHandlers;

/// <summary>
/// Translates unexpected exceptions into a <c>500 Internal Server Error</c> response with Problem Details.
/// </summary>
/// <param name="problemDetailsService">The service that writes the Problem Details response.</param>
/// <param name="logger">The logger.</param>
/// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
public sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger = logger;

    /// <inheritdoc />
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
