using Orders.Domain;

namespace Orders.Domain.Tests;

[TestFixture]
public class OrderTests
{
    [Test]
    public void Create_WithValidData_StartsPending()
    {
        var order = Order.Create("customer-001", 250m);

        Assert.Multiple(() =>
        {
            Assert.That(order.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(order.CustomerId, Is.EqualTo("customer-001"));
            Assert.That(order.TotalAmount, Is.EqualTo(250m));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
        });
    }

    [Test]
    public void MarkCompleted_FromPending_ChangesState()
    {
        var order = Order.Create("customer-001", 100m);

        order.MarkCompleted();

        Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
    }

    [Test]
    public void TerminalOrder_CannotTransitionAgain()
    {
        var order = Order.Create("customer-001", 100m);
        order.MarkCompleted();

        Assert.Throws<InvalidOperationException>(() => order.MarkPaymentFailed("declined"));
    }

    [Test]
    public void RejectInventory_StoresReason()
    {
        var order = Order.Create("customer-001", 100m);

        order.RejectInventory("SKU unavailable");

        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.InventoryRejected));
            Assert.That(order.FailureReason, Is.EqualTo("SKU unavailable"));
        });
    }
}
