using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.DTOs
{

    public sealed class MessageReactionsDetailsDto
    {
        public Guid MessageId { get; set; }
        public Guid CurrentUserId { get; set; }

        public List<ReactionTabDto> Tabs { get; set; } = new();
        public List<ReactionEntryDto> Entries { get; set; } = new();
    }
}
