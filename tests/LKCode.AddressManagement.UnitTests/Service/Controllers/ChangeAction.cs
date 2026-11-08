namespace LKCode.AddressManagement.UnitTests.Service.Controllers;

/// <summary>
/// The actions of the addresses controller that change an existing address and therefore require <c>If-Match</c>.
/// </summary>
public enum ChangeAction
{
    /// <summary>
    /// <c>PUT</c>: replaces all values.
    /// </summary>
    Update,

    /// <summary>
    /// <c>PATCH</c>: changes single values.
    /// </summary>
    Patch,

    /// <summary>
    /// <c>DELETE</c>: deletes the address.
    /// </summary>
    Delete,
}
