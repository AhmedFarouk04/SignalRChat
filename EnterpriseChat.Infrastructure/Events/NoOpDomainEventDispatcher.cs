using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;

namespace EnterpriseChat.Infrastructure.Events;

public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(
        IEnumerable<DomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
