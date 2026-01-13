using EnterpriseChat.Domain.Events;

namespace EnterpriseChat.Application.Interfaces;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IEnumerable<DomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
