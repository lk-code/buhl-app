using LKCode.AddressManagement.Domain.Contracts;

namespace LKCode.AddressManagement.Domain.Addresses;

/// <summary>
/// A postal address of a person. An address can only be created and changed through <see cref="Create"/>,
/// which keeps it valid and maintains the normalized fields and timestamps.
/// </summary>
public sealed class Address
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Address"/> class. Used by EF Core to materialize entities.
    /// </summary>
    private Address()
    {
    }

    /// <summary>
    /// Gets the unique identifier, a version 7 GUID.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the street.
    /// </summary>
    public string Street { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the house number.
    /// </summary>
    public string HouseNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the postal code as entered.
    /// </summary>
    public string PostalCode { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the city as entered.
    /// </summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the country as uppercase ISO 3166-1 alpha-2 code, e.g. <c>DE</c>.
    /// </summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the email address, or <see langword="null"/> if none is set.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Gets the city normalized by <see cref="ITextNormalizer.Normalize"/> for searching.
    /// </summary>
    public string NormalizedCity { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the postal code normalized by <see cref="ITextNormalizer.NormalizePostalCode"/> for searching.
    /// </summary>
    public string NormalizedPostalCode { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the point in time the address was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the point in time the address was last changed.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a new address.
    /// </summary>
    /// <param name="firstName">The first name.</param>
    /// <param name="lastName">The last name.</param>
    /// <param name="street">The street.</param>
    /// <param name="houseNumber">The house number.</param>
    /// <param name="postalCode">The postal code.</param>
    /// <param name="city">The city.</param>
    /// <param name="country">The country as ISO 3166-1 alpha-2 code.</param>
    /// <param name="email">The optional email address.</param>
    /// <param name="textNormalizer">The normalizer for the city and the postal code.</param>
    /// <param name="timeProvider">The provider of the current time.</param>
    /// <returns>The new address.</returns>
    public static Address Create(
        string firstName,
        string lastName,
        string street,
        string houseNumber,
        string postalCode,
        string city,
        string country,
        string? email,
        ITextNormalizer textNormalizer,
        TimeProvider timeProvider) => throw new NotImplementedException();
}
