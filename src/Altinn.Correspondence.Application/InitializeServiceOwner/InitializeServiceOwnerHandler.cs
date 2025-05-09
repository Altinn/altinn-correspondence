﻿using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeServiceOwner;

public class InitializeServiceOwnerHandler(IServiceOwnerRepository serviceOwnerRepository, IResourceManager resourceManager) : IHandler<InitializeServiceOwnerRequest, bool>
{
    public async Task<OneOf<bool, Error>> Process(InitializeServiceOwnerRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceOwnerId) || request.ServiceOwnerId.Length != 9)
        {
            return new Error(0, "Service owner id must be their organization number", System.Net.HttpStatusCode.BadRequest);
        }
        var couldCreateServiceOwner = await serviceOwnerRepository.InitializeNewServiceOwner(request.ServiceOwnerId, request.ServiceOwnerName, cancellationToken);
        if (!couldCreateServiceOwner)
        {
            return new Error(1, "Service owner already exists", System.Net.HttpStatusCode.Conflict);
        }
        var serviceOwner = await serviceOwnerRepository.GetServiceOwnerByOrgNo(request.ServiceOwnerId, cancellationToken);
        resourceManager.DeployStorageAccountsForServiceOwner(serviceOwner, cancellationToken);
        return true;
    }
}