using EnterpriseChat.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.DTOs
{
    public sealed class ReactionTabDto
    {
        public ReactionType? Type { get; set; } // null = All
        public int Count { get; set; }
    }
}
