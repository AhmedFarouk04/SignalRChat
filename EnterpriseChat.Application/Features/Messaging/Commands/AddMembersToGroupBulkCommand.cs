using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record AddMembersToGroupBulkCommand(
    RoomId RoomId,
    List<UserId> MemberIds, // قائمة الأعضاء
    UserId RequesterId
) : IRequest<Unit>;