using System.Net;
using System.Text;
using Customers.Application;
using Customers.Infrastructure;

namespace Customers.Infrastructure.Tests;

[TestFixture]
public sealed class BrasilApiPostalCodeLookupTests
{
    [Test]
    public async Task Lookup_maps_address_ibge_and_coordinates()
    {
        const string json = """
            {
              "cep": "89010025",
              "state": "SC",
              "city": "Blumenau",
              "neighborhood": "Centro",
              "street": "Rua Doutor Luiz de Freitas Melro",
              "service": "open-cep",
              "location": {
                "type": "Point",
                "coordinates": {
                  "longitude": "-49.0629788",
                  "latitude": "-26.9244749"
                }
              },
              "ibge": {
                "city": "4202404",
                "state": "42"
              }
            }
            """;

        using var client = CreateClient(HttpStatusCode.OK, json);
        var lookup = new BrasilApiPostalCodeLookup(client);

        var result = await lookup.LookupAsync("89010-025", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.PostalCode, Is.EqualTo("89010025"));
            Assert.That(result.City, Is.EqualTo("Blumenau"));
            Assert.That(result.State, Is.EqualTo("SC"));
            Assert.That(result.IbgeCityCode, Is.EqualTo("4202404"));
            Assert.That(result.Latitude, Is.EqualTo(-26.9244749m));
            Assert.That(result.Longitude, Is.EqualTo(-49.0629788m));
            Assert.That(result.Provider, Is.EqualTo("open-cep"));
        });
    }

    [Test]
    public async Task Lookup_accepts_missing_coordinates()
    {
        const string json = """
            {
              "cep": "89010025",
              "state": "SC",
              "city": "Blumenau",
              "neighborhood": "Centro",
              "street": "Rua Fixture",
              "location": {
                "type": "Point",
                "coordinates": {}
              }
            }
            """;

        using var client = CreateClient(HttpStatusCode.OK, json);
        var lookup = new BrasilApiPostalCodeLookup(client);

        var result = await lookup.LookupAsync("89010025", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Latitude, Is.Null);
            Assert.That(result.Longitude, Is.Null);
        });
    }

    [Test]
    public void Lookup_404_is_mapped_to_domain_specific_exception()
    {
        using var client = CreateClient(HttpStatusCode.NotFound, "{}");
        var lookup = new BrasilApiPostalCodeLookup(client);

        Assert.ThrowsAsync<PostalCodeNotFoundException>(async () =>
            await lookup.LookupAsync("89010025", CancellationToken.None));
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string body) =>
        new(new StubHandler(statusCode, body))
        {
            BaseAddress = new Uri("https://brasilapi.com.br/")
        };

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    RequestMessage = request
                });
    }
}
