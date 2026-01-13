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
        // 1️⃣ Save changes first
        await _context.SaveChangesAsync(cancellationToken);

        // 2️⃣ Collect domain events
        var domainEvents = _context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is Message)
            .SelectMany(e => ((Message)e.Entity).DomainEvents)
            .ToList();

        // 3️⃣ Dispatch events
        if (domainEvents.Any())
        {
            await _dispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        // 4️⃣ Clear events
        foreach (var entry in _context.ChangeTracker.Entries<Message>())
        {
            entry.Entity.ClearDomainEvents();
        }
    }
}
