using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Queries;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Data.Repositories;
using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.IntegrationTests.Fakes;

/// <summary>
/// Decorates the <see cref="AddressRepository"/> and holds every change, including a deletion, back until all
/// participants of the <paramref name="rendezvous"/> want to save.
/// </summary>
/// <remarks>
/// Concurrent requests that reach the repository have all passed the checks of the controller and the service with
/// the same version. Only the concurrency token of the database can then detect that they conflict.
/// </remarks>
public sealed class RendezvousAddressRepository(AddressRepository inner, Rendezvous rendezvous) : IAddressRepository
{
    public Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        inner.GetByIdAsync(id, cancellationToken);

    public Task<AddressPage> ListAsync(AddressListCriteria criteria, CancellationToken cancellationToken) =>
        inner.ListAsync(criteria, cancellationToken);

    public Task AddAsync(Address address, CancellationToken cancellationToken) =>
        inner.AddAsync(address, cancellationToken);

    public async Task<ChangeStatus> UpdateAsync(
        Address address,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        await rendezvous.ArriveAndWaitAsync(cancellationToken);

        return await inner.UpdateAsync(address, expectedVersion, cancellationToken);
    }
}
