using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Interfaces;

public interface IMessageBroadcaster
{
	Task MessageDeliveredAsync(
		MessageId messageId,
		UserId userId);

	Task MessageReadAsync(
		MessageId messageId,
		UserId userId);

    Task BroadcastMessageAsync(
        MessageDto message,
        IEnumerable<UserId> recipients);
}
