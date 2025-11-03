using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class ServiceOwnerRepository(ApplicationDbContext context, ILogger<IServiceOwnerRepository> logger) : IServiceOwnerRepository
    {
        public async Task<ServiceOwnerEntity?> GetServiceOwnerByOrgNo(string orgNo, CancellationToken cancellationToken)
        {
            return await context.ServiceOwners
                .AsNoTracking()
                .Include(so => so.StorageProviders)
                .SingleOrDefaultAsync(so => so.Id == orgNo, cancellationToken);
        }
        
        public async Task<ServiceOwnerEntity?> GetServiceOwnerByOrgCode(string orgCode, CancellationToken cancellationToken)
        {
            return await context.ServiceOwners
                .AsNoTracking()
                .Include(so => so.StorageProviders)
                .SingleOrDefaultAsync(so => so.Name == orgCode, cancellationToken);
        }

        public async Task<bool> InitializeNewServiceOwner(string orgNo, string name, CancellationToken cancellationToken)
        {
            var existingServiceOwner = await context.ServiceOwners
                .AsNoTracking()
                .SingleOrDefaultAsync(so => so.Id == orgNo, cancellationToken);
            if (existingServiceOwner != null)
            {
                logger.LogError("Cannot create service owner because it already exists with id {orgNo}", orgNo);
                return false;
            }
            var serviceOwner = new ServiceOwnerEntity
            {
                Id = orgNo,
                Name = name,
                StorageProviders = new List<StorageProviderEntity>()
            };

            await context.ServiceOwners.AddAsync(serviceOwner, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task InitializeStorageProvider(string orgNo, string storageAccountName, StorageProviderType storageType)
        {
            var serviceOwner = await context.ServiceOwners
                .Include(so => so.StorageProviders)
                .SingleOrDefaultAsync(so => so.Id == orgNo);

            if (serviceOwner == null)
            {
                throw new ArgumentException($"Service owner with organization number {orgNo} does not exist", nameof(orgNo));
            }

            var existingProvider = serviceOwner.StorageProviders
                .FirstOrDefault(sp => sp.StorageResourceName == storageAccountName && sp.Type == storageType);

            if (existingProvider == null)
            {
                var storageProvider = new StorageProviderEntity
                {
                    ServiceOwnerId = orgNo,
                    StorageResourceName = storageAccountName,
                    Type = storageType,
                    Created = DateTimeOffset.UtcNow,
                    Active = true
                };

                serviceOwner.StorageProviders.Add(storageProvider);
                await context.SaveChangesAsync();
            }
        }
    }
}
