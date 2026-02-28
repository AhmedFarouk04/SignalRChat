using EnterpriseChat.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.DTOs
{
    public sealed class ReactionEntryDto
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public ReactionType Type { get; set; }
        public DateTime CreatedAt { get; set; }  // ✨ أضفنا ده
        public bool IsMe { get; set; }
    }
}