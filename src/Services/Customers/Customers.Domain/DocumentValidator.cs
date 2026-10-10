using System.Text;

namespace Customers.Domain;

public static class DocumentValidator
{
    public static string NormalizeAndValidate(PersonType personType, string document)
    {
        if (!TryNormalize(document, out var normalized))
            throw new ArgumentException("Document contains unsupported characters.", nameof(document));

        var valid = personType switch
        {
            PersonType.Individual => IsValidCpf(normalized),
            PersonType.Company => IsValidCnpj(normalized),
            _ => false
        };

        if (!valid)
            throw new ArgumentException(
                personType == PersonType.Individual ? "CPF is invalid." : "CNPJ is invalid.",
                nameof(document));

        return normalized;
    }

    public static bool IsValidCpf(string document)
    {
        if (!TryNormalize(document, out var cpf) ||
            cpf.Length != 11 ||
            HasRepeatedDigits(cpf))
        {
            return false;
        }

        var first = CalculateDigit(cpf.AsSpan(0, 9), [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = CalculateDigit(cpf.AsSpan(0, 10), [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);

        return cpf[9] - '0' == first && cpf[10] - '0' == second;
    }

    public static bool IsValidCnpj(string document)
    {
        if (!TryNormalize(document, out var cnpj) ||
            cnpj.Length != 14 ||
            HasRepeatedDigits(cnpj))
        {
            return false;
        }

        var first = CalculateDigit(cnpj.AsSpan(0, 12), [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = CalculateDigit(cnpj.AsSpan(0, 13), [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);

        return cnpj[12] - '0' == first && cnpj[13] - '0' == second;
    }

    private static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character is '.' or '-' or '/' || char.IsWhiteSpace(character))
                continue;

            return false;
        }

        normalized = builder.ToString();
        return normalized.Length > 0;
    }

    private static int CalculateDigit(ReadOnlySpan<char> digits, ReadOnlySpan<int> weights)
    {
        var sum = 0;

        for (var index = 0; index < digits.Length; index++)
            sum += (digits[index] - '0') * weights[index];

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static bool HasRepeatedDigits(string value) =>
        value.All(character => character == value[0]);
}
