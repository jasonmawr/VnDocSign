using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Domain.Entities.Signing;

namespace VnDocSign.Application.Contracts.Dtos.Signing;

public sealed record ApproveRequest(Guid TaskId, Guid ActorUserId, string Pin, string? Comment); // Pin: rỗng => ký MOCK (PNG), có giá trị => ký số SSM thật
public sealed record RejectRequest(Guid TaskId, Guid ActorUserId, string? Comment);
public sealed record ClerkConfirmRequest(Guid DossierId, Guid ActorUserId);
public sealed record MyTaskItem(Guid TaskId, Guid DossierId, SlotKey SlotKey, int Order);

// === GĐ5: DTO chi tiết cho MyTasks (grouped) ===
public sealed record MyTaskListItem(
    Guid TaskId,
    Guid DossierId,
    string Code,
    string Title,
    DossierStatus Status,
    SlotKey SlotKey,
    int Order,
    DateTime CreatedAt
);

public sealed record MyTasksGroupedDto(
    IReadOnlyList<MyTaskListItem> Pending,
    IReadOnlyList<MyTaskListItem> Processed,
    IReadOnlyList<MyTaskListItem> Completed
);

