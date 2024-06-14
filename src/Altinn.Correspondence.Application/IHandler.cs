using OneOf;

namespace Altinn.Correspondence.Application;

internal interface IHandler<TRequest, TResponse>
{
    Task<OneOf<TResponse, Error>> Process(TRequest request, CancellationToken cancellationToken);
}
