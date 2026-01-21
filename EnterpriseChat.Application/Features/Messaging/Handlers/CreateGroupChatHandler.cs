using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class CreateGroupChatHandler
    : IRequestHandler<CreateGroupChatCommand, ChatRoom>
{
    private readonly IChatRoomRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGroupChatHandler(
        IChatRoomRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChatRoom> Handle(
        CreateGroupChatCommand command,
        CancellationToken cancellationToken)
    {
        var room = ChatRoom.CreateGroup(
            command.Name,
            command.CreatorId,
            command.Members);

        await _repository.AddAsync(room, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return room;
    }
}
