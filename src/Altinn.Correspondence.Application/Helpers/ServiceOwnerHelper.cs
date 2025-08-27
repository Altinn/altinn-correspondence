using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers;

public class ServiceOwnerHelper(
    IServiceOwnerRepository serviceOwnerRepository,
    ILogger<ServiceOwnerHelper> logger)
{
    /// <summary>
    /// Safely gets the ServiceOwnerId by checking if the ServiceOwner exists in the database.
    /// Returns null if the ServiceOwner doesn't exist to avoid foreign key violations.
    /// </summary>
    /// <param name="serviceOwnerOrgNumber">The organization number to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ServiceOwnerId if it exists, otherwise null</returns>
    public async Task<string?> GetSafeServiceOwnerIdAsync(string serviceOwnerOrgNumber, CancellationToken cancellationToken)
    {
        try
        {
            var orgNumber = serviceOwnerOrgNumber.WithoutPrefix();
            var serviceOwner = await serviceOwnerRepository.GetServiceOwnerByOrgNo(orgNumber, cancellationToken);
            
            if (serviceOwner == null)
            {
                logger.LogWarning("ServiceOwner not found for organization number {OrgNumber}. ServiceOwnerId will be set to null to avoid foreign key violation.", orgNumber.SanitizeForLogging());
                return null;
            }
            
            logger.LogDebug("Found ServiceOwner {ServiceOwnerId} for organization number {OrgNumber}", serviceOwner.Id.SanitizeForLogging(), orgNumber.SanitizeForLogging());
            return serviceOwner.Id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check ServiceOwner existence for organization number {OrgNumber}. ServiceOwnerId will be set to null.", serviceOwnerOrgNumber.WithoutPrefix().SanitizeForLogging());
            return null;
        }
    }

    /// <summary>
    /// Gets the sender URN format and safe ServiceOwnerId for the given organization number.
    /// </summary>
    /// <param name="serviceOwnerOrgNumber">The organization number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing the sender URN and the safe ServiceOwnerId (or null)</returns>
    public async Task<(string sender, string? serviceOwnerId)> GetSenderAndServiceOwnerIdAsync(string serviceOwnerOrgNumber, CancellationToken cancellationToken)
    {
        var sender = serviceOwnerOrgNumber.WithoutPrefix().WithUrnPrefix();
        var serviceOwnerId = await GetSafeServiceOwnerIdAsync(serviceOwnerOrgNumber, cancellationToken);
        return (sender, serviceOwnerId);
    }

    /// <summary>
    /// Gets the sender URN format, safe ServiceOwnerId, and migration status for the given organization number.
    /// </summary>
    /// <param name="serviceOwnerOrgNumber">The organization number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing the sender URN, the safe ServiceOwnerId (or null), and migration status (1 if exists, 2 if not)</returns>
    public async Task<(string sender, string? serviceOwnerId, int migrationStatus)> GetSenderServiceOwnerIdAndMigrationStatusAsync(string serviceOwnerOrgNumber, CancellationToken cancellationToken)
    {
        var sender = serviceOwnerOrgNumber.WithoutPrefix().WithUrnPrefix();
        var serviceOwnerId = await GetSafeServiceOwnerIdAsync(serviceOwnerOrgNumber, cancellationToken);
        
        // Set migration status: 1 if ServiceOwner exists, 2 if it doesn't
        // TODO: Remove this when the migration is complete
        var migrationStatus = serviceOwnerId != null ? 1 : 2;
        
        return (sender, serviceOwnerId, migrationStatus);
    }
}