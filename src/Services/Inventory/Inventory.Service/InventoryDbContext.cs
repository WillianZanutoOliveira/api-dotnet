using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : DbContext(options)
{
    public DbSet<InventoryDecision> Decisions => Set<InventoryDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryDecision>(entity =>
        {
            entity.ToTable("inventory_decisions");
            entity.HasKey(decision => decision.Id);
            entity.HasIndex(decision => decision.OrderId).IsUnique();
            entity.Property(decision => decision.Status).HasMaxLength(40).IsRequired();
            entity.Property(decision => decision.Reason).HasMaxLength(500);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
