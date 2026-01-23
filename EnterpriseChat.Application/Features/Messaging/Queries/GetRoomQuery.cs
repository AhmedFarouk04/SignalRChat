using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed record GetRoomQuery(
    RoomId RoomId,
    UserId UserId
) : IRequest<RoomDetailsDto>;
