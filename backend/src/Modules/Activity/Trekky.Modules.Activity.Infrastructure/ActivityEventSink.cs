using Trekky.Modules.Activity.Application.ActivityEvents;
using Trekky.Modules.Activity.Persistence;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.Activity.Infrastructure;

/// <summary>Stores every event the outbox dispatcher delivers in the activity log.</summary>
internal sealed class ActivityEventSink(ActivityEventWriter writer) : IActivityEventSink
{
    public Task WriteAsync(ActivityEnvelope envelope, CancellationToken cancellationToken)
        => writer.AppendAsync(ActivityEventMapper.FromEnvelope(envelope), cancellationToken);
}
