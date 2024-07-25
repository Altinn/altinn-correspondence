﻿namespace Altinn.Broker.Correspondence.Repositories;

public interface IResourceRightsService
{
    Task<bool> Exists(string resourceId, CancellationToken cancellationToken = default);
}