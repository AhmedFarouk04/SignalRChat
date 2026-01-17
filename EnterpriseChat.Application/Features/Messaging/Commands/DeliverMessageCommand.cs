using EnterpriseChat.Domain.ValueObjects;


namespace EnterpriseChat.Application.Features.Messaging.Commands
{
    public sealed record DeliverMessageCommand(
    MessageId MessageId,
    UserId UserId
);

}
