using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeResourceOwner;

public class InitializeServiceOwnerHandler(IServiceOwnerRepository serviceOwnerRepository, IResourceManager resourceManager) : IHandler<InitializeServiceOwnerRequest, bool>
{
    public async Task<OneOf<bool, Error>> Process(InitializeServiceOwnerRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceOwnerId) || request.ServiceOwnerId.Length != 9)
        {
            return new Error(0, "Service owner id must be their organization number", System.Net.HttpStatusCode.BadRequest);
        }
        await serviceOwnerRepository.InitializeServiceOwner(request.ServiceOwnerId, request.ServiceOwnerName, cancellationToken);
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(request.ServiceOwnerId, cancellationToken);
        resourceManager.CreateStorageProviders(serviceOwner, cancellationToken);
        return true;
    }
}
