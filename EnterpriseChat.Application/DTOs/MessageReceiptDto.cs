using EnterpriseChat.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.DTOs
{
    public class MessageReceiptDto
    {
        public Guid UserId { get; set; }
        public MessageStatus Status { get; set; } // Delivered or Read
    }
}
