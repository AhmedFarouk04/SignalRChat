using EnterpriseChat.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Domain.Events
{
    public class MessageEditedEvent : DomainEvent
    {
        public MessageId MessageId { get; }
        public RoomId RoomId { get; }
        public string NewContent { get; }

        public MessageEditedEvent(MessageId messageId, RoomId roomId, string newContent)
        {
            MessageId = messageId;
            RoomId = roomId;
            NewContent = newContent;
        }
    }
}
