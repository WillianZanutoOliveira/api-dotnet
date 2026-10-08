using System.Text;

namespace Customers.Domain;

public sealed class Address
{
    private Address() { }

    private Address(Guid customerId, AddressDetails details)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Apply(details);
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public AddressType Type { get; private set; }
    public bool IsPrimary { get; private set; }
    public string PostalCode { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string? Complement { get; private set; }
    public string? Neighborhood { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string Country { get; private set; } = "BR";
    public string? IbgeCityCode { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    internal static Address Create(Guid customerId, AddressDetails details) =>
        new(customerId, details);

    private void Apply(AddressDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        Type = details.Type;
        IsPrimary = details.IsPrimary;
        PostalCode = NormalizePostalCode(details.PostalCode);
        Street = Required(details.Street, nameof(details.Street), 200);
        Number = Required(details.Number, nameof(details.Number), 30);
        Complement = Optional(details.Complement, 120);
        Neighborhood = Optional(details.Neighborhood, 120);
        City = Required(details.City, nameof(details.City), 120);
        State = NormalizeState(details.State);
        Country = "BR";
        IbgeCityCode = Optional(details.IbgeCityCode, 10);
        Latitude = details.Latitude;
        Longitude = details.Longitude;

        if (Latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(details.Latitude));

        if (Longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(details.Longitude));
    }

    public static string NormalizePostalCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Postal code is required.", nameof(value));

        var builder = new StringBuilder(8);

        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character == '-' || char.IsWhiteSpace(character))
                continue;

            throw new ArgumentException("Postal code contains unsupported characters.", nameof(value));
        }

        if (builder.Length != 8)
            throw new ArgumentException("Postal code must contain exactly 8 digits.", nameof(value));

        return builder.ToString();
    }

    private static string NormalizeState(string value)
    {
        var normalized = Required(value, nameof(value), 2).ToUpperInvariant();

        if (normalized.Length != 2 || normalized.Any(character => !char.IsAsciiLetter(character)))
            throw new ArgumentException("State must contain exactly two letters.", nameof(value));

        return normalized;
    }

    private static string Required(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"Value exceeds {maxLength} characters.", parameterName);

        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"Value exceeds {maxLength} characters.", nameof(value));

        return normalized;
    }
}

public sealed record AddressDetails(
    AddressType Type,
    bool IsPrimary,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string? Neighborhood,
    string City,
    string State,
    string? IbgeCityCode,
    decimal? Latitude,
    decimal? Longitude);
