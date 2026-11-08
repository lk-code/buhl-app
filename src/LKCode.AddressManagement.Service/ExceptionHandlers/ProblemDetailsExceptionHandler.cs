using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LKCode.AddressManagement.Service.ExceptionHandlers;

/// <summary>
/// Translates unexpected exceptions into a <c>500 Internal Server Error</c> response with Problem Details.
/// </summary>
/// <param name="problemDetailsService">The service that writes the Problem Details response.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
/// The exception is logged completely, including the stack trace. The response only contains the status, the
/// default title and type of the status and the <c>traceId</c> that links it to the log; nothing about the
/// exception is exposed. Since .NET 10 the exception handler middleware no longer logs exceptions that a handler
/// has handled, so this handler is responsible for the log.
/// </remarks>
/// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
public sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService =
        problemDetailsService ?? throw new ArgumentNullException(nameof(problemDetailsService));

    private readonly ILogger<ProblemDetailsExceptionHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Logs the <paramref name="exception"/> and writes a <c>500 Internal Server Error</c> response with Problem
    /// Details.
    /// </summary>
    /// <param name="httpContext">The context of the request that failed.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the response was written; <see langword="false"/> if the client does not accept
    /// Problem Details (e.g. only <c>text/html</c>), so that the middleware answers with an empty response.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="httpContext"/> or <paramref name="exception"/> is <see langword="null"/>.
    /// </exception>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        LogUnhandledException(_logger, exception, httpContext.Request.Method, httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = StatusCodes.Status500InternalServerError },
        });
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "An unhandled exception occurred while processing {RequestMethod} {RequestPath}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string requestMethod,
        string? requestPath);
}
