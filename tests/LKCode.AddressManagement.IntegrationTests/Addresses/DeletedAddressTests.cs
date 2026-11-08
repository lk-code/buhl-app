using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests that a deleted address stays stored, marked as deleted, but behaves for every request as if it had never
/// existed.
/// </summary>
public class DeletedAddressTests
{
    private const string UnknownId = "0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00";

    private static readonly string[] _ifMatchValuesOfDeletedAddress = ["\"1\"", "\"2\"", "*"];

    private AddressManagementWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task DeleteAsync_KnownId_KeepsAddressStoredAndMarkedAsDeleted()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await DeleteAsync(created);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Address? stored = await _factory.FindStoredAddressAsync(created.Id);
        stored.Should().NotBeNull();
        stored!.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().NotBeNull();
        stored.DeletedAt!.Value.Should().BeOnOrAfter(created.UpdatedAt);
        stored.UpdatedAt.Should().Be(stored.DeletedAt.Value);
        stored.Version.Should().Be(2);
        stored.Should().BeEquivalentTo(
            created,
            options => options.Excluding(address => address.UpdatedAt).Excluding(address => address.Version));
    }

    [TestCaseSource(nameof(ChangeOfDeletedAddressCases))]
    public async Task ChangeAsync_DeletedAddress_ReturnsSameResponseAsUnknownIdAndKeepsDeletion(
        HttpMethod method,
        string ifMatch)
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage deletion = await DeleteAsync(created);
        Address? deleted = await _factory.FindStoredAddressAsync(created.Id);

        // Act
        using HttpResponseMessage response = await SendAsync(method, created.Id.ToString(), ifMatch);

        // Assert
        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage unknownResponse = await SendAsync(method, UnknownId, ifMatch);
        await AssertSameResponseAsync(response, unknownResponse);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
        (await _factory.FindStoredAddressAsync(created.Id)).Should().BeEquivalentTo(deleted);
    }

    [Test]
    public async Task GetAsync_DeletedAddress_ReturnsSameResponseAsUnknownId()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage deletion = await DeleteAsync(created);

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{AddressChangeRequests.Path}/{created.Id}");

        // Assert
        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage unknownResponse = await _client.GetAsync($"{AddressChangeRequests.Path}/{UnknownId}");
        await AssertSameResponseAsync(response, unknownResponse);
    }

    /// <summary>
    /// <c>PUT</c>, <c>PATCH</c> and <c>DELETE</c>, each with the entity tag of the version before the deletion
    /// (<c>"1"</c>), the version of the deletion (<c>"2"</c>) and <c>*</c>.
    /// </summary>
    private static IEnumerable<TestCaseData> ChangeOfDeletedAddressCases() =>
        from method in AddressChangeRequests.Methods
        from ifMatch in _ifMatchValuesOfDeletedAddress
        select new TestCaseData(method, ifMatch);

    /// <summary>
    /// Asserts that both responses are <c>404 Not Found</c> without <c>ETag</c> and with the same problem details,
    /// apart from the <c>traceId</c>.
    /// </summary>
    private static async Task AssertSameResponseAsync(HttpResponseMessage response, HttpResponseMessage expected)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        expected.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.ETag.Should().BeNull();
        (response.Content.Headers.ContentType?.MediaType).Should().Be(expected.Content.Headers.ContentType?.MediaType);
        (await ReadWithoutTraceIdAsync(response)).Should().Equal(await ReadWithoutTraceIdAsync(expected));
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadWithoutTraceIdAsync(
        HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .ToDictionary(property => property.Name, property => property.Value.GetRawText());
    }

    private Task<HttpResponseMessage> DeleteAsync(AddressResponse address) =>
        _client.SendValidAsync(HttpMethod.Delete, address.Id, address.ToETag());

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string id, string ifMatch) =>
        _client.SendWithIfMatchAsync(
            method,
            $"{AddressChangeRequests.Path}/{id}",
            AddressChangeRequests.CreateValidContent(method),
            ifMatch);
}
