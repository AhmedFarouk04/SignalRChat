using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed record GetOrCreatePrivateRoomCommand(
    UserId UserA,
    UserId UserB
) : IRequest<PrivateRoomDto>;
