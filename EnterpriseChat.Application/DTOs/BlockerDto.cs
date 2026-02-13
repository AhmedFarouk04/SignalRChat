using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.DTOs
{
    public record BlockerDto(
     Guid BlockerId,
     string? BlockerDisplayName,
     DateTime CreatedAt);
}
