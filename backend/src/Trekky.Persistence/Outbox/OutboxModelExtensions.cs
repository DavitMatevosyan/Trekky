using Microsoft.EntityFrameworkCore;
using Trekky.SharedKernel.Events;

namespace Trekky.Persistence.Outbox;

public static class OutboxModelExtensions
{
    public const string TableName = "outbox";

    /// <summary>Maps the module's <c>outbox</c> table in the context's default schema. Call from <c>OnModelCreating</c>.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable(TableName);
            outbox.HasKey(m => m.Id);
            outbox.Property(m => m.Id).ValueGeneratedNever();
            outbox.Property(m => m.EventType).HasMaxLength(64).IsRequired();
            outbox.Property(m => m.Envelope).HasColumnType("jsonb").IsRequired();
            outbox.HasIndex(m => m.OrgId);
            // The dispatcher's scan: unprocessed rows in event order.
            outbox.HasIndex(m => m.Id).HasDatabaseName("ix_outbox_pending").HasFilter("processed_at IS NULL");
        });

        return modelBuilder;
    }

    /// <summary>
    /// Queues an event in the outbox. It is written by the same <c>SaveChanges</c> as the change it describes, so both
    /// commit or neither does.
    /// </summary>
    public static void AddActivityEvent(this DbContext context, ActivityEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Set<OutboxMessage>().Add(OutboxMessage.From(envelope));
    }
}
