using System.Runtime.CompilerServices;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Service.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LKCode.AddressManagement.UnitTests.Service.ExceptionHandlers;

public class ProblemDetailsExceptionHandlerTests
{
    private const string ExceptionMessage = "Sensitive internal detail";
    private const string RequestPath = "/api/addresses";
    private const string TraceIdentifier = "0HN7TRACE:00000001";
    private const string InternalServerErrorTitle = "An error occurred while processing your request.";
    private const string InternalServerErrorType = "https://tools.ietf.org/html/rfc9110#section-15.6.1";

    private ServiceProvider _serviceProvider = null!;
    private IProblemDetailsService _problemDetailsService = null!;
    private FakeLogger<ProblemDetailsExceptionHandler> _logger = null!;
    private ProblemDetailsExceptionHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().AddOptions().AddProblemDetails().BuildServiceProvider();
        _problemDetailsService = _serviceProvider.GetRequiredService<IProblemDetailsService>();
        _logger = new FakeLogger<ProblemDetailsExceptionHandler>();
        _handler = new ProblemDetailsExceptionHandler(_problemDetailsService, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public void Constructor_NullProblemDetailsService_ThrowsArgumentNullException()
    {
        // Arrange
        IProblemDetailsService problemDetailsService = null!;

        // Act
        Action act = () => _ = new ProblemDetailsExceptionHandler(problemDetailsService, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("problemDetailsService");
    }

    [Test]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        ILogger<ProblemDetailsExceptionHandler> logger = null!;

        // Act
        Action act = () => _ = new ProblemDetailsExceptionHandler(_problemDetailsService, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Test]
    public async Task TryHandleAsync_NullHttpContext_ThrowsArgumentNullException()
    {
        // Arrange
        HttpContext httpContext = null!;

        // Act
        Func<Task> act = async () => await _handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException(ExceptionMessage),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("httpContext");
    }

    [Test]
    public async Task TryHandleAsync_NullException_ThrowsArgumentNullException()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();
        Exception exception = null!;

        // Act
        Func<Task> act = async () => await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("exception");
    }

    [TestCaseSource(nameof(UnexpectedExceptions))]
    public async Task TryHandleAsync_UnexpectedException_ReturnsTrue(Exception exception)
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
    }

    [TestCaseSource(nameof(UnexpectedExceptions))]
    public async Task TryHandleAsync_UnexpectedException_SetsStatusCode500(Exception exception)
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [TestCaseSource(nameof(UnexpectedExceptions))]
    public async Task TryHandleAsync_UnexpectedException_WritesProblemDetailsContentType(Exception exception)
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        httpContext.Response.ContentType.Should().Be("application/problem+json");
    }

    [TestCaseSource(nameof(UnexpectedExceptions))]
    public async Task TryHandleAsync_UnexpectedException_WritesStatusTitleAndType(Exception exception)
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        JsonElement body = await ReadJsonBodyAsync(httpContext);
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);
        body.GetProperty("title").GetString().Should().Be(InternalServerErrorTitle);
        body.GetProperty("type").GetString().Should().Be(InternalServerErrorType);
    }

    [TestCaseSource(nameof(UnexpectedExceptions))]
    public async Task TryHandleAsync_UnexpectedException_WritesTraceId(Exception exception)
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        JsonElement body = await ReadJsonBodyAsync(httpContext);
        body.GetProperty("traceId").GetString().Should().Be(TraceIdentifier);
    }

    [TestCaseSource(nameof(UnexpectedExceptions))]
    public async Task TryHandleAsync_UnexpectedException_DoesNotExposeExceptionDetails(Exception exception)
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        JsonElement body = await ReadJsonBodyAsync(httpContext);
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("type", "title", "status", "traceId");
        string text = await ReadTextBodyAsync(httpContext);
        text.Should().NotContain(ExceptionMessage)
            .And.NotContain(exception.GetType().Name)
            .And.NotContain(nameof(ThrowAndCatch));
    }

    [TestCaseSource(nameof(UnexpectedExceptions))]
    public async Task TryHandleAsync_UnexpectedException_LogsErrorWithException(Exception exception)
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        FakeLogRecord record = _logger.Collector.GetSnapshot().Should().ContainSingle().Which;
        record.Level.Should().Be(LogLevel.Error);
        record.Id.Id.Should().Be(1);
        record.Message.Should().Be($"An unhandled exception occurred while processing GET {RequestPath}");
        record.Exception.Should().BeSameAs(exception);
        record.GetStructuredStateValue("RequestMethod").Should().Be("GET");
        record.GetStructuredStateValue("RequestPath").Should().Be(RequestPath);
    }

    [Test]
    public async Task TryHandleAsync_ClientDoesNotAcceptJson_ReturnsFalse()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();
        httpContext.Request.Headers.Accept = "text/html";

        // Act
        bool handled = await _handler.TryHandleAsync(
            httpContext,
            ThrowAndCatch(new InvalidOperationException(ExceptionMessage)),
            CancellationToken.None);

        // Assert
        handled.Should().BeFalse();
        httpContext.Response.Body.Length.Should().Be(0);
    }

    [Test]
    public async Task TryHandleAsync_ClientDoesNotAcceptJson_LogsErrorWithException()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext();
        httpContext.Request.Headers.Accept = "text/html";
        Exception exception = ThrowAndCatch(new InvalidOperationException(ExceptionMessage));

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        FakeLogRecord record = _logger.Collector.GetSnapshot().Should().ContainSingle().Which;
        record.Level.Should().Be(LogLevel.Error);
        record.Exception.Should().BeSameAs(exception);
    }

    /// <summary>
    /// Unexpected exceptions, each thrown once so that it has a stack trace. An
    /// <see cref="OperationCanceledException"/> is unexpected as long as the client has not aborted the request.
    /// </summary>
    private static IEnumerable<TestCaseData> UnexpectedExceptions()
    {
        yield return new TestCaseData(ThrowAndCatch(new InvalidOperationException(ExceptionMessage)))
            .SetArgDisplayNames(nameof(InvalidOperationException));
        yield return new TestCaseData(ThrowAndCatch(new KeyNotFoundException(ExceptionMessage)))
            .SetArgDisplayNames(nameof(KeyNotFoundException));
        yield return new TestCaseData(ThrowAndCatch(new OperationCanceledException(ExceptionMessage)))
            .SetArgDisplayNames(nameof(OperationCanceledException));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception ThrowAndCatch(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    private DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = _serviceProvider,
            TraceIdentifier = TraceIdentifier,
        };
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = RequestPath;
        httpContext.Response.Body = new MemoryStream();

        return httpContext;
    }

    private static async Task<JsonElement> ReadJsonBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);

        return document.RootElement.Clone();
    }

    private static async Task<string> ReadTextBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }
}
