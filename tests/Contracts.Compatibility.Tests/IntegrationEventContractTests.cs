using DistributedCommerce.Contracts;
using NUnit.Framework;

namespace Contracts.Compatibility.Tests;

[TestFixture]
public sealed class IntegrationEventContractTests
{
    [TestCaseSource(nameof(EventContracts))]
    public void Published_event_shape_is_backward_compatibility_guarded(
        Type eventType,
        string[] expectedProperties)
    {
        var actualProperties = eventType
            .GetProperties()
            .OrderBy(property => property.MetadataToken)
            .Select(property => $"{property.Name}:{FriendlyTypeName(property.PropertyType)}")
            .ToArray();

        Assert.That(actualProperties, Is.EqualTo(expectedProperties));
    }

    private static IEnumerable<TestCaseData> EventContracts()
    {
        yield return Contract<OrderSubmitted>(
            "EventId:Guid",
            "OrderId:Guid",
            "CustomerId:String",
            "Items:IReadOnlyCollection<OrderLineMessage>",
            "TotalAmount:Decimal",
            "OccurredAt:DateTimeOffset");

        yield return Contract<InventoryReserved>(
            "EventId:Guid",
            "OrderId:Guid",
            "TotalAmount:Decimal",
            "OccurredAt:DateTimeOffset");

        yield return Contract<InventoryRejected>(
            "EventId:Guid",
            "OrderId:Guid",
            "Reason:String",
            "OccurredAt:DateTimeOffset");

        yield return Contract<PaymentAuthorized>(
            "EventId:Guid",
            "OrderId:Guid",
            "PaymentId:String",
            "Amount:Decimal",
            "OccurredAt:DateTimeOffset");

        yield return Contract<PaymentFailed>(
            "EventId:Guid",
            "OrderId:Guid",
            "Reason:String",
            "OccurredAt:DateTimeOffset");
    }

    private static TestCaseData Contract<T>(params string[] properties) =>
        new(typeof(T), properties)
        {
            TestName = $"{typeof(T).Name}_contract_shape_is_stable"
        };

    private static string FriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var separator = type.Name.IndexOf((char)96);
        var genericName = type.Name[..separator];
        var arguments = string.Join(
            ",",
            type.GetGenericArguments().Select(FriendlyTypeName));

        return $"{genericName}<{arguments}>";
    }
}
