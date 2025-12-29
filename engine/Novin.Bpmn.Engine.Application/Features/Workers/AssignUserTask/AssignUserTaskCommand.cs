using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;


public sealed record AssignUserTaskCommand(
    Guid WorkerId,
    string? Assignee,
    string? CandidateGroups,
    int? Priority,
    DateTime? DueDateUtc,
    string AssignedBy
) : IRequest<AssignUserTaskResult>;

public enum AssignUserTaskResult { Ok, NotFound, InvalidState }

public sealed record CompleteUserTaskCommand(
    Guid WorkerId,
    string CompletedBy,
    Dictionary<string, string> Result,
    string? Comment
) : IRequest<CompleteUserTaskResult>;

public enum CompleteUserTaskResult { Ok, NotFound, InvalidState, TokenNotWaiting }

public sealed record CompleteServiceTaskCommand(
    Guid WorkerId,
    string CompletedByClientId,
    Dictionary<string, string> Result
) : IRequest<CompleteServiceTaskResult>;

public enum CompleteServiceTaskResult { Ok, NotFound, InvalidState, TokenNotWaiting }

public sealed record FailServiceTaskCommand(
    Guid WorkerId,
    string FailedByClientId,
    string ErrorMessage,
    string? ErrorCode
) : IRequest<FailServiceTaskResult>;

public enum FailServiceTaskResult { Ok, NotFound, InvalidState, TokenNotWaiting }