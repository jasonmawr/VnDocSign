using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Signing;

namespace VnDocSign.Application.Contracts.Dtos.Signing;

public sealed record ApproveRequest(Guid TaskId, Guid ActorUserId, string Pin, string? Comment); // Pin hiện chưa dùng (PHASE 1)
public sealed record RejectRequest(Guid TaskId, Guid ActorUserId, string? Comment);
public sealed record ClerkConfirmRequest(Guid DossierId, Guid ActorUserId);
public sealed record MyTaskItem(Guid TaskId, Guid DossierId, SlotKey SlotKey, int Order);

