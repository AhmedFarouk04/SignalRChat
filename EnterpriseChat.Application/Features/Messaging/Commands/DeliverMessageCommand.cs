using EnterpriseChat.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.Features.Messaging.Commands
{
    public sealed record DeliverMessageCommand(
    MessageId MessageId,
    UserId UserId
);

}
