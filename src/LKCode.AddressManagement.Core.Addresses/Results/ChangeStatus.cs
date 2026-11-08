namespace LKCode.AddressManagement.Core.Addresses.Results;

/// <summary>
/// The outcome of a change or deletion of an address that is checked against the version of the address.
/// </summary>
public enum ChangeStatus
{
    /// <summary>
    /// The change or deletion was saved.
    /// </summary>
    Success,

    /// <summary>
    /// There is no address with the id, e.g. because it was deleted in the meantime. Nothing was saved.
    /// </summary>
    NotFound,

    /// <summary>
    /// The stored version of the address differs from the expected version, because the address was changed in the
    /// meantime. Nothing was saved.
    /// </summary>
    VersionMismatch,
}
