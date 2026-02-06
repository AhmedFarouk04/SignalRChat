using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.DTOs
{
    public class MessageReceiptStatsModel
    {
        public int TotalRecipients { get; set; }
        public int DeliveredCount { get; set; }
        public int ReadCount { get; set; }
        public List<UserSummaryDto> DeliveredUsers { get; set; } = new();
        public List<UserSummaryDto> ReadUsers { get; set; } = new();
    }
}
