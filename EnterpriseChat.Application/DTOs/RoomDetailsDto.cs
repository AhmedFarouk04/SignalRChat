namespace EnterpriseChat.Application.DTOs;

public sealed record RoomDetailsDto(
    Guid Id,
    string Name,
    string Type
);
