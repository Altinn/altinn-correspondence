﻿using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application;

internal interface IHandler<TRequest, TResponse>
{
    Task<OneOf<TResponse, Error>> Process(TRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken);
}
