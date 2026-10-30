using System.Text.Json;
using System.Text.Json.Nodes;
using Trekky.Modules.Activity.Application.ActivityEvents;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.Activity.Tests;

public sealed class ActivityEventMapperTests
{
    [Fact]
    public void Copies_every_envelope_field_into_the_log_row()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 26, 9, 41, 12, TimeSpan.Zero);
        var (org, user, project) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var envelope = ActivityEnvelope.Create(
            "ProjectCreated", occurredAt, org, EventActor.User(user), new EventEntity("project", project, "TRK"), project,
            new JsonObject { ["key"] = "TRK", ["name"] = "Trekky" });

        var row = ActivityEventMapper.FromEnvelope(envelope);

        Assert.Equal(envelope.EventId, row.EventId);
        Assert.Equal(org, row.OrgId);
        Assert.Equal("ProjectCreated", row.EventType);
        Assert.Equal(1, row.SchemaVersion);
        Assert.Equal(occurredAt, row.OccurredAt);
        Assert.Equal("user", row.ActorType);
        Assert.Equal(user, row.ActorId);
        Assert.Equal("project", row.EntityType);
        Assert.Equal(project, row.EntityId);
        Assert.Equal("TRK", row.EntityKey);
        Assert.Equal(project, row.ProjectId);
        using var payload = JsonDocument.Parse(row.Payload);
        Assert.Equal("Trekky", payload.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void Event_ids_are_uuid_v7_from_the_time_of_the_change()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        var envelope = ActivityEnvelope.Create(
            "ProjectCreated", occurredAt, Guid.CreateVersion7(), EventActor.User(Guid.CreateVersion7()),
            new EventEntity("project", Guid.CreateVersion7(), "TRK"), null, []);

        Assert.Equal(7, envelope.EventId.Version);
        // The first 48 bits are the Unix time in milliseconds.
        var bytes = envelope.EventId.ToByteArray(bigEndian: true);
        var millis = ((long)bytes[0] << 40) | ((long)bytes[1] << 32) | ((long)bytes[2] << 24) | ((long)bytes[3] << 16) | ((long)bytes[4] << 8) | bytes[5];
        Assert.Equal(occurredAt.ToUnixTimeMilliseconds(), millis);
    }
}
