using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Customers.Application;
using Customers.Domain;

namespace Customers.Infrastructure;

public sealed class BrasilApiPostalCodeLookup(HttpClient httpClient) : IPostalCodeLookup
{
    public async Task<PostalCodeLookupResult> LookupAsync(
        string postalCode,
        CancellationToken cancellationToken)
    {
        var normalized = Address.NormalizePostalCode(postalCode);

        using var response = await httpClient.GetAsync(
            $"api/cep/v2/{normalized}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new PostalCodeNotFoundException(normalized);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BrasilApiCepResponse>(
            cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("BrasilAPI returned an empty CEP response.");

        return new PostalCodeLookupResult(
            Address.NormalizePostalCode(Required(payload.Cep, "cep")),
            payload.Street?.Trim() ?? string.Empty,
            EmptyToNull(payload.Neighborhood),
            Required(payload.City, "city"),
            Required(payload.State, "state").ToUpperInvariant(),
            EmptyToNull(payload.Ibge?.City),
            ParseCoordinate(payload.Location?.Coordinates?.Latitude),
            ParseCoordinate(payload.Location?.Coordinates?.Longitude),
            string.IsNullOrWhiteSpace(payload.Service) ? "brasilapi" : payload.Service.Trim());
    }

    private static decimal? ParseCoordinate(string? value) =>
        decimal.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var coordinate)
            ? coordinate
            : null;

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new HttpRequestException($"BrasilAPI response is missing required field '{field}'.")
            : value.Trim();

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BrasilApiCepResponse(
        [property: JsonPropertyName("cep")] string? Cep,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("neighborhood")] string? Neighborhood,
        [property: JsonPropertyName("street")] string? Street,
        [property: JsonPropertyName("service")] string? Service,
        [property: JsonPropertyName("location")] BrasilApiLocation? Location,
        [property: JsonPropertyName("ibge")] BrasilApiIbge? Ibge);

    private sealed record BrasilApiLocation(
        [property: JsonPropertyName("coordinates")] BrasilApiCoordinates? Coordinates);

    private sealed record BrasilApiCoordinates(
        [property: JsonPropertyName("longitude")] string? Longitude,
        [property: JsonPropertyName("latitude")] string? Latitude);

    private sealed record BrasilApiIbge(
        [property: JsonPropertyName("city")] string? City);
}
