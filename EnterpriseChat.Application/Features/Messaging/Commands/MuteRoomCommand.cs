using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record MuteRoomCommand(RoomId RoomId, UserId UserId) : IRequest<Unit>;
