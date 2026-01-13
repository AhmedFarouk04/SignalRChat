using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record MuteRoomCommand(
    RoomId RoomId,
    UserId UserId);
