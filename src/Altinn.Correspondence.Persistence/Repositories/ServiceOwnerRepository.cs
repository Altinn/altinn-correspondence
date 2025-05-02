using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class ServiceOwnerRepository : IServiceOwnerRepository
    {
        private readonly ApplicationDbContext _context;

        public ServiceOwnerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceOwnerEntity?> GetServiceOwner(string orgNo, CancellationToken cancellationToken)
        {
            return await _context.ServiceOwners
                .Include(so => so.StorageProviders)
                .SingleOrDefaultAsync(so => so.Id == orgNo, cancellationToken);
        }

        public async Task InitializeServiceOwner(string orgNo, string name, CancellationToken cancellationToken)
        {
            var existingServiceOwner = await _context.ServiceOwners
                .SingleOrDefaultAsync(so => so.Id == orgNo, cancellationToken);

            if (existingServiceOwner == null)
            {
                var serviceOwner = new ServiceOwnerEntity
                {
                    Id = orgNo,
                    Name = name,
                    StorageProviders = new List<StorageProviderEntity>()
                };

                await _context.ServiceOwners.AddAsync(serviceOwner, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task InitializeStorageProvider(string orgNo, string storageAccountName, StorageProviderType storageType)
        {
            var serviceOwner = await _context.ServiceOwners
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
                await _context.SaveChangesAsync();
            }
        }
    }
}
