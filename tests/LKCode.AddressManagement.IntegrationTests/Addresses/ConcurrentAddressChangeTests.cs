using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests two truly concurrent changes of the same address. Both requests pass all checks with the same version and
/// wait for each other right before saving (see <see cref="RendezvousAddressRepository"/>); only the concurrency token
/// of EF Core InMemory can detect the conflict.
/// </summary>
public class ConcurrentAddressChangeTests
{
    private const int Participants = 2;

    private AddressManagementWebApplicationFactory _baseFactory = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Rendezvous _rendezvous = null!;

    [SetUp]
    public void SetUp()
    {
        _rendezvous = new Rendezvous(Participants);
        _baseFactory = new AddressManagementWebApplicationFactory();
        _factory = _baseFactory.WithRendezvousBeforeSaving(_rendezvous);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _baseFactory.DisposeAsync();
    }

    [TestCase("\"1\"")]
    [TestCase("*")]
    public async Task PutAsync_TwoConcurrentChangesOfSameVersion_SavesOneAndReturns412ForOther(string ifMatch)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        (HttpResponseMessage berlin, HttpResponseMessage hamburg) = await SendConcurrentlyAsync(
            () => PutCityAsync(created.Id, "Berlin", ifMatch),
            () => PutCityAsync(created.Id, "Hamburg", ifMatch));

        // Assert
        using (berlin)
        using (hamburg)
        {
            _rendezvous.Arrived.Should().Be(Participants);
            new[] { berlin.StatusCode, hamburg.StatusCode }
                .Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.PreconditionFailed]);
            HttpResponseMessage saved = berlin.StatusCode == HttpStatusCode.OK ? berlin : hamburg;
            HttpResponseMessage rejected = berlin.StatusCode == HttpStatusCode.OK ? hamburg : berlin;
            AddressResponse savedAddress = (await saved.Content.ReadFromJsonAsync<AddressResponse>())!;
            savedAddress.Version.Should().Be(2);
            (await _factory.FindAddressAsync(created.Id)).Should().Be(savedAddress);
            await AssertPreconditionFailedAsync(rejected);
        }
    }

    [Test]
    public async Task PutAndPatchAsync_ConcurrentChangesOfSameVersion_SavesOneAndReturns412ForOther()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        (HttpResponseMessage put, HttpResponseMessage patch) = await SendConcurrentlyAsync(
            () => PutCityAsync(created.Id, "Hamburg", created.ToETag()),
            () => _client.SendValidAsync(HttpMethod.Patch, created.Id, created.ToETag()));

        // Assert
        using (put)
        using (patch)
        {
            _rendezvous.Arrived.Should().Be(Participants);
            new[] { put.StatusCode, patch.StatusCode }
                .Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.PreconditionFailed]);
            HttpResponseMessage saved = put.StatusCode == HttpStatusCode.OK ? put : patch;
            AddressResponse savedAddress = (await saved.Content.ReadFromJsonAsync<AddressResponse>())!;
            (await _factory.FindAddressAsync(created.Id)).Should().Be(savedAddress);
        }
    }

    [Test]
    public async Task DeleteAsync_TwoConcurrentDeletionsOfSameVersion_DeletesOnceAndReturns404ForOther()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        (HttpResponseMessage first, HttpResponseMessage second) = await SendConcurrentlyAsync(
            () => _client.SendValidAsync(HttpMethod.Delete, created.Id, created.ToETag()),
            () => _client.SendValidAsync(HttpMethod.Delete, created.Id, created.ToETag()));

        // Assert
        using (first)
        using (second)
        {
            _rendezvous.Arrived.Should().Be(Participants);
            new[] { first.StatusCode, second.StatusCode }
                .Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.NotFound]);
            (await _factory.FindAddressAsync(created.Id)).Should().BeNull();
        }
    }

    [Test]
    public async Task PutAndDeleteAsync_ConcurrentOfSameVersion_ExecutesOneAndRejectsOther()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        (HttpResponseMessage put, HttpResponseMessage delete) = await SendConcurrentlyAsync(
            () => PutCityAsync(created.Id, "Hamburg", created.ToETag()),
            () => _client.SendValidAsync(HttpMethod.Delete, created.Id, created.ToETag()));

        // Assert
        using (put)
        using (delete)
        {
            _rendezvous.Arrived.Should().Be(Participants);
            AddressResponse? stored = await _factory.FindAddressAsync(created.Id);
            if (put.StatusCode == HttpStatusCode.OK)
            {
                delete.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
                stored.Should().Be(await put.Content.ReadFromJsonAsync<AddressResponse>());
            }
            else
            {
                put.StatusCode.Should().Be(HttpStatusCode.NotFound);
                delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
                stored.Should().BeNull();
            }
        }
    }

    /// <summary>
    /// Starts both requests without waiting for the first one, so that they run concurrently.
    /// </summary>
    private static async Task<(HttpResponseMessage First, HttpResponseMessage Second)> SendConcurrentlyAsync(
        Func<Task<HttpResponseMessage>> first,
        Func<Task<HttpResponseMessage>> second)
    {
        Task<HttpResponseMessage> firstTask = Task.Run(first);
        Task<HttpResponseMessage> secondTask = Task.Run(second);
        HttpResponseMessage[] responses = await Task.WhenAll(firstTask, secondTask);

        return (responses[0], responses[1]);
    }

    private static async Task AssertPreconditionFailedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Precondition Failed");
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.PreconditionFailed);
    }

    private Task<AddressResponse> SeedAsync() => _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

    private Task<HttpResponseMessage> PutCityAsync(Guid id, string city, string ifMatch) =>
        _client.SendWithIfMatchAsync(
            HttpMethod.Put,
            $"{AddressChangeRequests.Path}/{id}",
            JsonContent.Create(AddressPayloads.ValidWith("city", city)),
            ifMatch);
}
