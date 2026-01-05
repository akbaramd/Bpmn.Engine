// Domain/ValueObjects/UserTaskActor.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed record UserTaskActor(
    string UserId,
    IReadOnlyCollection<string> Groups,
    IReadOnlyCollection<string> Roles)
{
    public UserTaskActor(string userId)
        : this(userId, Array.Empty<string>(), Array.Empty<string>()) { }

    public string UserId { get; init; } =
        !string.IsNullOrWhiteSpace(UserId) ? UserId : throw new ArgumentException("UserId cannot be empty.", nameof(UserId));

    public IReadOnlyCollection<string> Groups { get; init; } =
        Normalize(Groups);

    public IReadOnlyCollection<string> Roles { get; init; } =
        Normalize(Roles);

    private static IReadOnlyCollection<string> Normalize(IReadOnlyCollection<string> values)
    {
        if (values is null || values.Count == 0) return Array.Empty<string>();

        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
