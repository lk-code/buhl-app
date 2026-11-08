using System.Diagnostics.CodeAnalysis;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.Core.Addresses.Results;

/// <summary>
/// The outcome of a change of an address that is checked against the version of the address: the status and, if
/// the change was saved, the changed address.
/// </summary>
public sealed record AddressChangeResult
{
    /// <summary>
    /// Gets the result of a change that was not saved because there is no address with the id.
    /// </summary>
    public static AddressChangeResult NotFound => throw new NotImplementedException();

    /// <summary>
    /// Gets the result of a change that was not saved because the address has a different version than expected.
    /// </summary>
    public static AddressChangeResult VersionMismatch => throw new NotImplementedException();

    /// <summary>
    /// Gets the status of the change.
    /// </summary>
    public ChangeStatus Status { get; }

    /// <summary>
    /// Gets the changed address if the change was saved; otherwise <see langword="null"/>.
    /// </summary>
    public AddressResponse? Address { get; }

    /// <summary>
    /// Gets a value indicating whether the change was saved; <see cref="Address"/> is set exactly then.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Address))]
    public bool IsSuccess => throw new NotImplementedException();

    /// <summary>
    /// Creates the result of a change that was saved.
    /// </summary>
    /// <param name="address">The changed address.</param>
    /// <returns>The result with the status <see cref="ChangeStatus.Success"/> and the changed address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    public static AddressChangeResult Success(AddressResponse address) => throw new NotImplementedException();
}
