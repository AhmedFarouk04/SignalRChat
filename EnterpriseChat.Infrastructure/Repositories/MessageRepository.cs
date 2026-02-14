using EnterpriseChat.Application.Features.Messaging;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using EnterpriseChat.Domain.Common;

namespace EnterpriseChat.Infrastructure.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly ChatDbContext _context;

    public MessageRepository(ChatDbContext context)
    {
        _context = context;
    }
    public async Task<int> GetUnreadCountAsync(RoomId roomId, DateTime lastReadAt, UserId userId, CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.RoomId == roomId
                        && m.CreatedAt > lastReadAt
                        && m.SenderId != userId
                        && !m.IsDeleted)
            .CountAsync(ct);
    }

    public async Task<int> GetTotalUnreadCountAsync(RoomId roomId, UserId userId, CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.RoomId == roomId
                        && m.SenderId != userId
                        && !m.IsDeleted)
            .CountAsync(ct);
    }
    public async Task<IReadOnlyList<Message>> GetUndeliveredForUserAsync(
    RoomId roomId,
    UserId userId,
    CancellationToken ct = default)
    {
        // نجيب كل الرسائل اللي:
        // 1. في الروم ده
        // 2. مش من المستخدم نفسه
        // 3. الـ receipt بتاعته لسه < Delivered
        return await _context.Messages
            .Include(m => m.Receipts)
            .Where(m => m.RoomId == roomId
                && m.SenderId != userId
                && m.Receipts.Any(r => r.UserId == userId && r.Status < MessageStatus.Delivered))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }
    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _context.Messages.AddAsync(message, cancellationToken);
    }

    public async Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken)
    {
        return await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == MessageId.From(messageId), cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetByRoomAsync(
        RoomId roomId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetByRoomForUpdateAsync(
        RoomId roomId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Include(m => m.Receipts)
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetCreatedAtAsync(MessageId messageId, CancellationToken ct = default)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => (DateTime?)m.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<(MessageId Id, UserId SenderId)>> GetUnreadUpToAsync(
        RoomId roomId,
        DateTime lastCreatedAt,
        UserId readerId,
        int take,
        CancellationToken ct = default)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m =>
                m.RoomId == roomId &&
                m.CreatedAt <= lastCreatedAt &&
                m.SenderId != readerId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                HasUnread = m.Receipts.Any(r => r.UserId == readerId && r.Status < MessageStatus.Read)
            })
            .Where(x => x.HasUnread)
            .Select(x => new ValueTuple<MessageId, UserId>(x.Id, x.SenderId))
            .ToListAsync(ct);
    }

    public async Task<int> BulkMarkReadUpToAsync(
        RoomId roomId,
        DateTime lastCreatedAt,
        UserId readerId,
        CancellationToken ct = default)
    {
        // EF Core 7+: ExecuteUpdateAsync
        return await _context.MessageReceipts
            .Where(r =>
                r.UserId == readerId &&
                r.Status < MessageStatus.Read &&
                _context.Messages.Any(m =>
                    m.Id == r.MessageId &&
                    m.RoomId == roomId &&
                    m.CreatedAt <= lastCreatedAt))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, MessageStatus.Read)
                    .SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
                ct);
    }

    public async Task<(RoomId RoomId, UserId SenderId)?> GetRoomAndSenderAsync(
        MessageId id,
        CancellationToken ct = default)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new ValueTuple<RoomId, UserId>(m.RoomId, m.SenderId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(RoomId roomId, UserId userId, CancellationToken ct = default)
    {
        // ✅ المقارنات كلها ValueObjects (بدون .Value)
        return await (
            from r in _context.MessageReceipts.AsNoTracking()
            join m in _context.Messages.AsNoTracking() on r.MessageId equals m.Id
            where m.RoomId == roomId
                  && r.UserId == userId
                  && r.Status < MessageStatus.Read
                  && m.SenderId != userId
            select r
        ).CountAsync(ct);
    }

    public async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(
          IEnumerable<Guid> roomIds,
          UserId userId,
          CancellationToken ct = default)
    {
        var ids = roomIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();
        var json = JsonSerializer.Serialize(ids);
        var sql = @"
SELECT m.RoomId, COUNT(*) AS [Count]
FROM MessageReceipts r
INNER JOIN Messages m ON r.MessageId = m.Id
INNER JOIN OPENJSON(@json) WITH (RoomId UNIQUEIDENTIFIER '$') j ON m.RoomId = j.RoomId
WHERE r.UserId = @userId
  AND r.Status < @readStatus
  AND m.SenderId != @userId
GROUP BY m.RoomId";
        var paramJson = new Microsoft.Data.SqlClient.SqlParameter("@json", json);
        var paramUserId = new Microsoft.Data.SqlClient.SqlParameter("@userId", userId.Value);
        var paramReadStatus = new Microsoft.Data.SqlClient.SqlParameter("@readStatus", (int)MessageStatus.Read);
        var results = await _context.Database
            .SqlQueryRaw<UnreadDto>(sql, paramJson, paramUserId, paramReadStatus)
            .ToListAsync(ct);
        return results.ToDictionary(r => r.RoomId, r => r.Count);
    }

    //    public async Task<Dictionary<Guid, Message?>> GetLastMessagesAsync(
    //        IEnumerable<Guid> roomIds,
    //        CancellationToken ct = default)
    //    {
    //        var ids = roomIds.Distinct().ToList();
    //        if (ids.Count == 0) return new Dictionary<Guid, Message?>();
    //        var json = JsonSerializer.Serialize(ids);
    //        var sql = @"
    //WITH Ranked AS (
    //    SELECT
    //        m.Id,
    //        m.RoomId,
    //        m.SenderId,
    //        m.Content,
    //        m.CreatedAt,
    //        ROW_NUMBER() OVER (PARTITION BY m.RoomId ORDER BY m.CreatedAt DESC, m.Id DESC) AS rn
    //    FROM Messages m
    //    INNER JOIN OPENJSON(@json) WITH (RoomId UNIQUEIDENTIFIER '$') j ON m.RoomId = j.RoomId
    //)
    //SELECT
    //    m.Id,
    //    m.RoomId,
    //    m.SenderId,
    //    m.Content,
    //    m.CreatedAt
    //FROM Ranked m
    //WHERE m.rn = 1";
    //        var paramJson = new Microsoft.Data.SqlClient.SqlParameter("@json", json);
    //        var messages = await _context.Messages
    //            .FromSqlRaw(sql, paramJson)
    //            .AsNoTracking()
    //            .ToListAsync(ct);
    //        return messages
    //            .ToDictionary(m => m.RoomId.Value, m => m);
    //    }
  public async Task<IEnumerable<(Guid MessageId, Guid SenderId)>> GetMessageIdsAndSendersUpToAsync(
    RoomId roomId, 
    DateTime upTo, 
    CancellationToken ct = default)
{
    // ✅ استخدم await مباشرة من غير ContinueWith
    var messages = await _context.Messages
        .Where(m => m.RoomId == roomId && m.CreatedAt <= upTo)
        .OrderBy(m => m.CreatedAt)
        .Select(m => new 
        { 
            MessageId = m.Id.Value, 
            SenderId = m.SenderId.Value 
        })
        .ToListAsync(ct);
    
    return messages.Select(m => (m.MessageId, m.SenderId));
}
    public async Task<Dictionary<Guid, LastMessageInfo>> GetLastMessagesAsync(
    IReadOnlyList<Guid> roomIds,
    CancellationToken ct)
    {
        var ids = roomIds?.Distinct().ToList() ?? new();
        if (ids.Count == 0) return new Dictionary<Guid, LastMessageInfo>();

        var json = JsonSerializer.Serialize(ids);

        var sql = @"
;WITH Ranked AS (
    SELECT
        m.Id,
        m.RoomId,
        m.SenderId,
        m.Content,
        m.CreatedAt,
        ROW_NUMBER() OVER (PARTITION BY m.RoomId ORDER BY m.CreatedAt DESC, m.Id DESC) AS rn
    FROM Messages m
    INNER JOIN OPENJSON(@json) WITH (RoomId UNIQUEIDENTIFIER '$') j
        ON m.RoomId = j.RoomId
)
SELECT
    r.RoomId,
    r.Id        AS MessageId,
    r.SenderId,
    r.Content,
    r.CreatedAt,
    ISNULL(x.TotalRecipients, 0) AS TotalRecipients,
    ISNULL(x.DeliveredCount, 0)  AS DeliveredCount,
    ISNULL(x.ReadCount, 0)       AS ReadCount,
    ISNULL(x.MaxStatus, 1)       AS MaxStatus
FROM Ranked r
OUTER APPLY (
    SELECT
        COUNT(*) AS TotalRecipients,
        SUM(CASE WHEN CAST(rr.Status AS int) >= 2 THEN 1 ELSE 0 END) AS DeliveredCount,
        SUM(CASE WHEN CAST(rr.Status AS int) >= 3 THEN 1 ELSE 0 END) AS ReadCount,
        MAX(CAST(rr.Status AS int)) AS MaxStatus
    FROM MessageReceipts rr
    WHERE rr.MessageId = r.Id
) x
WHERE r.rn = 1
";


        var paramJson = new SqlParameter("@json", json);

        // ✅ نقرأ النتيجة كـ DTO بسيط
        var rows = await _context.Database
            .SqlQueryRaw<LastMessageRow>(sql, paramJson)
            .ToListAsync(ct);

        var dict = new Dictionary<Guid, LastMessageInfo>();

        foreach (var r in rows)
        {
            dict[r.RoomId] = new LastMessageInfo
            {
                RoomId = new RoomId(r.RoomId),
                Id = MessageId.From(r.MessageId),
                SenderId = new UserId(r.SenderId),
                Content = r.Content,
                CreatedAt = r.CreatedAt,
                TotalRecipients = r.TotalRecipients,
                DeliveredCount = r.DeliveredCount,
                ReadCount = r.ReadCount
            };

        }

        return dict;
    }

    // في MessageRepository.cs

   

    public async Task<Message?> GetByIdWithReceiptsAsync(MessageId messageId, CancellationToken ct = default)
    {
        return await _context.Messages
            .Include(m => m.Receipts)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);
    }

    public async Task<IReadOnlyList<UserId>> GetRoomMemberIdsAsync(RoomId roomId, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Where(r => r.Id == roomId)
            .SelectMany(r => r.Members)
            .Select(m => m.UserId)
            .ToListAsync(ct);
    }
}

// ✅ DTO بسيط للـ unread counts (record عشان يكون lightweight)
public record UnreadDto(Guid RoomId, int Count);
public sealed class LastMessageRow
{
    public Guid RoomId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public int TotalRecipients { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int MaxStatus { get; set; }
}
