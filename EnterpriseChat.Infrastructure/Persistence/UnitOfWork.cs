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

    // في Infrastructure/Persistence/UnitOfWork.cs
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[UOW] ========== CommitAsync START ==========");
        Console.WriteLine($"[UOW] Time: {DateTime.UtcNow:HH:mm:ss.fff}");

        try
        {
            // 1. شوف إيه اللي بيتغير قبل الحفظ
            var entries = _context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                           e.State == EntityState.Modified ||
                           e.State == EntityState.Deleted)
                .ToList();

            Console.WriteLine($"[UOW] Tracked entities: {entries.Count}");

            foreach (var entry in entries)
            {
                Console.WriteLine($"[UOW]   - {entry.Entity.GetType().Name}: {entry.State}");

                // لو الـ entity هي ChatRoom، اعرض التفاصيل
                if (entry.Entity is ChatRoom room)
                {
                    var originalName = entry.OriginalValues.GetValue<string>(nameof(ChatRoom.Name));
                    var currentName = entry.CurrentValues.GetValue<string>(nameof(ChatRoom.Name));

                    Console.WriteLine($"[UOW]   📌 ChatRoom {room.Id.Value}:");
                    Console.WriteLine($"[UOW]       Original Name: '{originalName}'");
                    Console.WriteLine($"[UOW]       Current Name : '{currentName}'");
                    Console.WriteLine($"[UOW]       HasChanges   : {entry.Properties.Any(p => p.IsModified)}");
                }
            }

            // 2. جرب الحفظ
            Console.WriteLine($"[UOW] Calling SaveChangesAsync...");
            var result = await _context.SaveChangesAsync(CancellationToken.None); // مؤقتاً بنستخدم None
            Console.WriteLine($"[UOW] SaveChangesAsync returned: {result}");

            if (result == 0 && entries.Any())
            {
                Console.WriteLine($"[UOW] ⚠️ WARNING: No rows affected but {entries.Count} entities were tracked!");
            }

            // 3. domain events زي ما انت عامل
            var domainEvents = _context.ChangeTracker
                .Entries()
                .Where(e => e.Entity is Message)
                .SelectMany(e => ((Message)e.Entity).DomainEvents)
                .ToList();

            if (domainEvents.Any())
            {
                Console.WriteLine($"[UOW] Dispatching {domainEvents.Count} domain events");
                await _dispatcher.DispatchAsync(domainEvents, cancellationToken);
            }

            foreach (var entry in _context.ChangeTracker.Entries<Message>())
                entry.Entity.ClearDomainEvents();

            Console.WriteLine($"[UOW] ========== CommitAsync END (Success) ==========");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UOW] ❌ ERROR in CommitAsync:");
            Console.WriteLine($"[UOW]    Type: {ex.GetType().Name}");
            Console.WriteLine($"[UOW]    Message: {ex.Message}");
            Console.WriteLine($"[UOW]    StackTrace: {ex.StackTrace}");
            throw;
        }
    }
}