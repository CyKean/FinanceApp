namespace FinanceApp.Domain.Common;

using FinanceApp.Domain.Enums;

public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public int Version { get; protected set; }
    public SyncStatus SyncStatus { get; protected set; }
    public DateTime? LastSyncedAt { get; protected set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsDeleted = false;
        Version = 1;
        SyncStatus = SyncStatus.PendingCreate;
    }

    protected Entity(Guid id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsDeleted = false;
        Version = 1;
        SyncStatus = SyncStatus.PendingCreate;
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateTimestamp()
    {
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void MarkAsPendingCreate()
    {
        SyncStatus = SyncStatus.PendingCreate;
        UpdateTimestamp();
    }

    public void MarkAsPendingUpdate()
    {
        if (SyncStatus == SyncStatus.Synced || SyncStatus == SyncStatus.PendingCreate)
            SyncStatus = SyncStatus.PendingUpdate;
        UpdateTimestamp();
    }

    public void MarkAsPendingDelete()
    {
        SyncStatus = SyncStatus.PendingDelete;
        UpdateTimestamp();
    }

    public void MarkAsSynced()
    {
        SyncStatus = SyncStatus.Synced;
        LastSyncedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void MarkAsFailed()
    {
        SyncStatus = SyncStatus.Failed;
        UpdateTimestamp();
    }

    /// <summary>
    /// Adopts server state during pull-merge (last-write-wins).
    /// Marks the row synced so the outbox does not re-push it.
    /// <para>
    /// The id is taken rather than assumed because these ids are minted by the
    /// client and used as the sync key on both sides. An entity built from a server
    /// row keeps a freshly generated id by default, which means the next sync
    /// cannot match the row it came from: it builds another copy, pushes that back
    /// up, and the account multiplies on every sync.
    /// </para>
    /// </summary>
    public void AdoptRemoteState(Guid id, DateTime createdAt, DateTime updatedAt, int version, bool isDeleted)
    {
        Id = id;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
        IsDeleted = isDeleted;
        SyncStatus = SyncStatus.Synced;
        LastSyncedAt = DateTime.UtcNow;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Entity? a, Entity? b)
    {
        if (a is null && b is null)
            return true;

        if (a is null || b is null)
            return false;

        return a.Equals(b);
    }

    public static bool operator !=(Entity? a, Entity? b)
    {
        return !(a == b);
    }
}