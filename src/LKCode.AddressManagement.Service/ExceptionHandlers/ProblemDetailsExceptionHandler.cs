using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LKCode.AddressManagement.Service.ExceptionHandlers;

/// <summary>
/// Translates unexpected exceptions into a <c>500 Internal Server Error</c> response with Problem Details.
/// </summary>
/// <param name="problemDetailsService">The service that writes the Problem Details response.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
/// <para>
/// The exception is logged completely, including the stack trace. The response only contains the status, the
/// default title and type of the status and the <c>traceId</c> that links it to the log; nothing about the
/// exception is exposed. Since .NET 10 the exception handler middleware no longer logs exceptions that a handler
/// has handled, so this handler is responsible for the log.
/// </para>
/// <para>
/// An <see cref="OperationCanceledException"/> of a request that the client aborted is not an error: it is logged
/// as information and answered with <c>499 Client Closed Request</c>. The exception handler middleware of .NET 10
/// already treats client aborts this way before it calls any handler; the check here keeps the handler correct on
/// its own.
/// </para>
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
    /// Details, or sets <c>499 Client Closed Request</c> without a body if the client aborted the request.
    /// </summary>
    /// <param name="httpContext">The context of the request that failed.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the exception was handled; <see langword="false"/> if the client does not accept
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

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(_logger, httpContext.Request.Method, httpContext.Request.Path.Value);

            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }

            return true;
        }

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

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Request {RequestMethod} {RequestPath} was aborted by the client")]
    private static partial void LogRequestAborted(ILogger logger, string requestMethod, string? requestPath);
}
