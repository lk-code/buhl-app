using AwesomeAssertions;
using LKCode.AddressManagement.IntegrationTests.Fakes;
using LKCode.AddressManagement.Service.ExceptionHandlers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace LKCode.AddressManagement.IntegrationTests;

public class LoggingTests
{
    private const string RequestLogTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    private const string UnknownPath = "/does-not-exist";
    private const string ThrowingPath = "/" + ThrowingController.Path;

    private CollectingLogEventSink _sink = null!;
    private AddressManagementWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _loggingFactory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _sink = new CollectingLogEventSink();
        _factory = new AddressManagementWebApplicationFactory();
        _loggingFactory = _factory.WithThrowingController().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(_sink)));
        _client = _loggingFactory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _loggingFactory.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task GetAsync_UnknownRoute_LogsRequestAsInformation()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(UnknownPath);

        // Assert
        LogEvent? logEvent = await _sink.WaitForAsync(IsRequestLog);
        logEvent.Should().NotBeNull();
        logEvent.Level.Should().Be(LogEventLevel.Information);
        GetScalar(logEvent, "RequestMethod").Should().Be("GET");
        GetScalar(logEvent, "RequestPath").Should().Be(UnknownPath);
        GetScalar(logEvent, "StatusCode").Should().Be(404);
    }

    [Test]
    public async Task GetAsync_UnexpectedException_LogsRequestAsError()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(ThrowingController.Path);

        // Assert
        LogEvent? logEvent = await _sink.WaitForAsync(IsRequestLog);
        logEvent.Should().NotBeNull();
        logEvent.Level.Should().Be(LogEventLevel.Error);
        GetScalar(logEvent, "RequestMethod").Should().Be("GET");
        GetScalar(logEvent, "RequestPath").Should().Be(ThrowingPath);
        GetScalar(logEvent, "StatusCode").Should().Be(500);
    }

    [Test]
    public async Task GetAsync_UnexpectedException_LogsExceptionOfHandler()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(ThrowingController.Path);

        // Assert
        LogEvent? logEvent = await _sink.WaitForAsync(logEvent =>
            Equals(GetScalar(logEvent, "SourceContext"), typeof(ProblemDetailsExceptionHandler).FullName));
        logEvent.Should().NotBeNull();
        logEvent.Level.Should().Be(LogEventLevel.Error);
        logEvent.Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be(ThrowingController.ExceptionMessage);
        GetScalar(logEvent, "RequestMethod").Should().Be("GET");
        GetScalar(logEvent, "RequestPath").Should().Be(ThrowingPath);
    }

    private static bool IsRequestLog(LogEvent logEvent) => logEvent.MessageTemplate.Text == RequestLogTemplate;

    private static object? GetScalar(LogEvent logEvent, string propertyName) =>
        logEvent.Properties.TryGetValue(propertyName, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value
            : null;
}
