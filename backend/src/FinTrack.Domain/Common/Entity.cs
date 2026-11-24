namespace FinTrack.Domain.Common;

/// <summary>
/// Base type for aggregate roots and entities. Identifiers are UUIDv7 so they are
/// time-ordered, which keeps B-tree indexes compact compared with random GUIDs.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}

/// <summary>Entities whose creation and modification timestamps are stamped by the persistence layer.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Raised when an operation would violate a business invariant.</summary>
public sealed class DomainException(string message) : Exception(message);
