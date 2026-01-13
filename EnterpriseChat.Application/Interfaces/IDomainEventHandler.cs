namespace EnterpriseChat.Application.Interfaces;

public interface IDomainEventHandler<in TDomainEvent>
{
    Task Handle(
        TDomainEvent domainEvent,
        CancellationToken cancellationToken);
}
