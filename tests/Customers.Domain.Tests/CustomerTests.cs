using Customers.Domain;

namespace Customers.Domain.Tests;

[TestFixture]
public sealed class CustomerTests
{
    [Test]
    public void Create_individual_normalizes_cpf_phone_and_address()
    {
        var customer = Customer.Create(
            PersonType.Individual,
            "111.444.777-35",
            "Cliente Fixture",
            null,
            new DateOnly(1990, 1, 1),
            null,
            null,
            null,
            "FIXTURE@example.com",
            "(44) 99999-0000",
            [PrimaryAddress()]);

        Assert.Multiple(() =>
        {
            Assert.That(customer.Document, Is.EqualTo("11144477735"));
            Assert.That(customer.Email, Is.EqualTo("fixture@example.com"));
            Assert.That(customer.Phone, Is.EqualTo("44999990000"));
            Assert.That(customer.Addresses.Single().PostalCode, Is.EqualTo("87000000"));
            Assert.That(customer.IsActive, Is.True);
        });
    }

    [Test]
    public void Create_company_requires_valid_cnpj_and_legal_name()
    {
        var customer = Customer.Create(
            PersonType.Company,
            "11.222.333/0001-81",
            "Comercio Fixture",
            "Comercio Fixture LTDA",
            null,
            new DateOnly(2020, 1, 1),
            "ISENTO",
            null,
            "contato@example.com",
            "44999990000",
            [PrimaryAddress()]);

        Assert.Multiple(() =>
        {
            Assert.That(customer.PersonType, Is.EqualTo(PersonType.Company));
            Assert.That(customer.Document, Is.EqualTo("11222333000181"));
            Assert.That(customer.LegalName, Is.EqualTo("Comercio Fixture LTDA"));
        });
    }

    [TestCase("000.000.000-00")]
    [TestCase("123.456.789-00")]
    public void Invalid_cpf_is_rejected(string cpf)
    {
        Assert.Throws<ArgumentException>(() =>
            Customer.Create(
                PersonType.Individual,
                cpf,
                "Cliente Fixture",
                null,
                null,
                null,
                null,
                null,
                "fixture@example.com",
                "44999990000",
                [PrimaryAddress()]));
    }

    [Test]
    public void Customer_requires_exactly_one_primary_address()
    {
        var addresses = new[]
        {
            PrimaryAddress() with { IsPrimary = false },
            PrimaryAddress() with { Type = AddressType.Shipping, IsPrimary = false }
        };

        Assert.Throws<ArgumentException>(() =>
            Customer.Create(
                PersonType.Individual,
                "11144477735",
                "Cliente Fixture",
                null,
                null,
                null,
                null,
                null,
                "fixture@example.com",
                "44999990000",
                addresses));
    }

    private static AddressDetails PrimaryAddress() =>
        new(
            AddressType.Primary,
            true,
            "87000-000",
            "Rua Fixture",
            "100",
            null,
            "Centro",
            "Maringa",
            "PR",
            "4115200",
            -23.4205m,
            -51.9333m);
}
