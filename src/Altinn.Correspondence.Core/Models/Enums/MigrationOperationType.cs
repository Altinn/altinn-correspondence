namespace Altinn.Correspondence.Core.Models.Enums;

/// <summary>
/// Defines the type of operation being performed for correspondence migration/sync
/// </summary>
public enum MigrationOperationType
{
    /// <summary>
    /// Synchronization operation
    /// </summary>
    Sync,
    
    /// <summary>
    /// Re-migration operation
    /// </summary>
    Remigrate
}
