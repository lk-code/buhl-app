using System.Runtime.CompilerServices;
using LKCode.AddressManagement.Domain.Contracts;

namespace LKCode.AddressManagement.Domain.Addresses;

/// <summary>
/// A postal address of a person. An address can only be created through <see cref="Create"/> and changed through
/// <see cref="Update"/>, which keep it valid and maintain the normalized fields and timestamps.
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
    /// Gets the version of the address: 1 after <see cref="Create"/>, increased by one with every change.
    /// </summary>
    /// <remarks>
    /// The version detects concurrent changes: a change based on an outdated version must be rejected.
    /// </remarks>
    public long Version { get; private set; }

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
    /// <remarks>
    /// The values are trimmed, the country is converted to uppercase and a blank email address is stored as
    /// <see langword="null"/>. The values are expected to be validated before; invalid values are a programming error.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// A required value, <paramref name="textNormalizer"/> or <paramref name="timeProvider"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A required value is empty or consists only of whitespace, or a value exceeds its maximum length defined in
    /// <see cref="AddressConstraints"/>.
    /// </exception>
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
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(textNormalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset now = timeProvider.GetUtcNow();
        var address = new Address
        {
            Id = Guid.CreateVersion7(now),
            CreatedAt = now,
        };
        address.SetValues(
            firstName,
            lastName,
            street,
            houseNumber,
            postalCode,
            city,
            country,
            email,
            textNormalizer,
            now);

        return address;
    }

    /// <summary>
    /// Changes all values of the address.
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
    /// <remarks>
    /// The values are cleaned like in <see cref="Create"/>. The normalized fields and <see cref="UpdatedAt"/> are
    /// updated, <see cref="Id"/> and <see cref="CreatedAt"/> stay unchanged. If a value is invalid, the address stays
    /// unchanged.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// A required value, <paramref name="textNormalizer"/> or <paramref name="timeProvider"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A required value is empty or consists only of whitespace, or a value exceeds its maximum length defined in
    /// <see cref="AddressConstraints"/>.
    /// </exception>
    public void Update(
        string firstName,
        string lastName,
        string street,
        string houseNumber,
        string postalCode,
        string city,
        string country,
        string? email,
        ITextNormalizer textNormalizer,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(textNormalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        SetValues(
            firstName,
            lastName,
            street,
            houseNumber,
            postalCode,
            city,
            country,
            email,
            textNormalizer,
            timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Checks and cleans all values first and assigns them afterwards, so that the address stays unchanged if a value
    /// is invalid.
    /// </summary>
    private void SetValues(
        string firstName,
        string lastName,
        string street,
        string houseNumber,
        string postalCode,
        string city,
        string country,
        string? email,
        ITextNormalizer textNormalizer,
        DateTimeOffset now)
    {
        firstName = RequireText(firstName, AddressConstraints.FirstNameMaxLength);
        lastName = RequireText(lastName, AddressConstraints.LastNameMaxLength);
        street = RequireText(street, AddressConstraints.StreetMaxLength);
        houseNumber = RequireText(houseNumber, AddressConstraints.HouseNumberMaxLength);
        postalCode = RequireText(postalCode, AddressConstraints.PostalCodeMaxLength);
        city = RequireText(city, AddressConstraints.CityMaxLength);
        country = RequireText(country, AddressConstraints.CountryMaxLength).ToUpperInvariant();
        email = OptionalText(email, AddressConstraints.EmailMaxLength);
        string normalizedCity = textNormalizer.Normalize(city);
        string normalizedPostalCode = textNormalizer.NormalizePostalCode(postalCode);

        FirstName = firstName;
        LastName = lastName;
        Street = street;
        HouseNumber = houseNumber;
        PostalCode = postalCode;
        City = city;
        Country = country;
        Email = email;
        NormalizedCity = normalizedCity;
        NormalizedPostalCode = normalizedPostalCode;
        UpdatedAt = now;
    }

    private static string RequireText(
        string? value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        return EnsureMaxLength(value.Trim(), maxLength, parameterName);
    }

    private static string? OptionalText(
        string? value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) =>
        string.IsNullOrWhiteSpace(value) ? null : EnsureMaxLength(value.Trim(), maxLength, parameterName);

    private static string EnsureMaxLength(string value, int maxLength, string? parameterName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"The value must not exceed {maxLength} characters.", parameterName);
        }

        return value;
    }
}
