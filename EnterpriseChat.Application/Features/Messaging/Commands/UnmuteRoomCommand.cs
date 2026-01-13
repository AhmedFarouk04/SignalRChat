using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record UnmuteRoomCommand(
    RoomId RoomId,
    UserId UserId);
