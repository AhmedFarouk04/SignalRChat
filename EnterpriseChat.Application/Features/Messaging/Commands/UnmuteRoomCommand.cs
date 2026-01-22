using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Commands;

public sealed record UnmuteRoomCommand(RoomId RoomId, UserId UserId) : IRequest<Unit>;
