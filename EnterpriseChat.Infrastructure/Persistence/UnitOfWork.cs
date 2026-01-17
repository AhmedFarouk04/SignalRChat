using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ChatDbContext _context;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWork(
        ChatDbContext context,
        IDomainEventDispatcher dispatcher)
    {
        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);

        var domainEvents = _context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is Message)
            .SelectMany(e => ((Message)e.Entity).DomainEvents)
            .ToList();

        if (domainEvents.Any())
        {
            await _dispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        foreach (var entry in _context.ChangeTracker.Entries<Message>())
        {
            entry.Entity.ClearDomainEvents();
        }
    }
}
