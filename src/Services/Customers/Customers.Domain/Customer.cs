using System.Net.Mail;
using System.Text;

namespace Customers.Domain;

public sealed class Customer
{
    private Customer() { }

    private Customer(
        PersonType personType,
        string document,
        string displayName,
        string? legalName,
        DateOnly? birthDate,
        DateOnly? foundationDate,
        string? stateRegistration,
        string? municipalRegistration,
        string email,
        string phone,
        IReadOnlyCollection<AddressDetails> addresses)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        IsActive = true;

        ApplyDetails(
            personType,
            document,
            displayName,
            legalName,
            birthDate,
            foundationDate,
            stateRegistration,
            municipalRegistration,
            email,
            phone);

        ReplaceAddresses(addresses);
    }

    public Guid Id { get; private set; }
    public PersonType PersonType { get; private set; }
    public string Document { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? LegalName { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public DateOnly? FoundationDate { get; private set; }
    public string? StateRegistration { get; private set; }
    public string? MunicipalRegistration { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<Address> Addresses { get; private set; } = [];

    public static Customer Create(
        PersonType personType,
        string document,
        string displayName,
        string? legalName,
        DateOnly? birthDate,
        DateOnly? foundationDate,
        string? stateRegistration,
        string? municipalRegistration,
        string email,
        string phone,
        IReadOnlyCollection<AddressDetails> addresses) =>
        new(
            personType,
            document,
            displayName,
            legalName,
            birthDate,
            foundationDate,
            stateRegistration,
            municipalRegistration,
            email,
            phone,
            addresses);

    public void Update(
        PersonType personType,
        string document,
        string displayName,
        string? legalName,
        DateOnly? birthDate,
        DateOnly? foundationDate,
        string? stateRegistration,
        string? municipalRegistration,
        string email,
        string phone,
        IReadOnlyCollection<AddressDetails> addresses,
        bool isActive)
    {
        if (personType != PersonType)
            throw new InvalidOperationException("Person type cannot be changed after creation.");

        ApplyDetails(
            personType,
            document,
            displayName,
            legalName,
            birthDate,
            foundationDate,
            stateRegistration,
            municipalRegistration,
            email,
            phone);

        ReplaceAddresses(addresses);
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void ApplyDetails(
        PersonType personType,
        string document,
        string displayName,
        string? legalName,
        DateOnly? birthDate,
        DateOnly? foundationDate,
        string? stateRegistration,
        string? municipalRegistration,
        string email,
        string phone)
    {
        PersonType = personType;
        Document = DocumentValidator.NormalizeAndValidate(personType, document);
        DisplayName = Required(displayName, nameof(displayName), 200);
        Email = NormalizeEmail(email);
        Phone = NormalizePhone(phone);

        if (personType == PersonType.Individual)
        {
            if (!string.IsNullOrWhiteSpace(legalName) ||
                foundationDate is not null ||
                !string.IsNullOrWhiteSpace(stateRegistration) ||
                !string.IsNullOrWhiteSpace(municipalRegistration))
            {
                throw new ArgumentException("Company-only fields cannot be used for an individual.");
            }

            if (birthDate is not null && birthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                throw new ArgumentOutOfRangeException(nameof(birthDate), "Birth date must be in the past.");

            LegalName = null;
            BirthDate = birthDate;
            FoundationDate = null;
            StateRegistration = null;
            MunicipalRegistration = null;
            return;
        }

        if (personType != PersonType.Company)
            throw new ArgumentOutOfRangeException(nameof(personType));

        LegalName = Required(legalName ?? string.Empty, nameof(legalName), 200);
        BirthDate = null;
        FoundationDate = foundationDate;
        StateRegistration = Optional(stateRegistration, 30);
        MunicipalRegistration = Optional(municipalRegistration, 30);

        if (foundationDate is not null && foundationDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentOutOfRangeException(nameof(foundationDate), "Foundation date cannot be in the future.");
    }

    private void ReplaceAddresses(IReadOnlyCollection<AddressDetails> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        if (addresses.Count is < 1 or > 10)
            throw new ArgumentException("A customer must have between 1 and 10 addresses.", nameof(addresses));

        if (addresses.Count(address => address.IsPrimary) != 1)
            throw new ArgumentException("Exactly one address must be marked as primary.", nameof(addresses));

        Addresses.Clear();

        foreach (var details in addresses)
            Addresses.Add(Address.Create(Id, details));
    }

    private static string NormalizeEmail(string value)
    {
        var normalized = Required(value, nameof(value), 254).ToLowerInvariant();

        if (!MailAddress.TryCreate(normalized, out _))
            throw new ArgumentException("Email is invalid.", nameof(value));

        return normalized;
    }

    private static string NormalizePhone(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Phone is required.", nameof(value));

        var digits = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
                continue;
            }

            if (character is '+' or '(' or ')' or '-' || char.IsWhiteSpace(character))
                continue;

            throw new ArgumentException("Phone contains unsupported characters.", nameof(value));
        }

        if (digits.Length is < 10 or > 13)
            throw new ArgumentException("Phone must contain between 10 and 13 digits.", nameof(value));

        return digits.ToString();
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
