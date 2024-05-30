using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatusCommand;

public class UpdateCorrespondenceStatusCommandHandler : IHandler<UpdateCorrespondenceStatusCommandRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    public UpdateCorrespondenceStatusCommandHandler(ICorrespondenceRepository correspondenceRepository)
    {
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusCommandRequest request, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var currentStatus = await _correspondenceRepository.GetLatestStatusByCorrespondenceId(request.CorrespondenceId, cancellationToken);
        Console.WriteLine(currentStatus?.Status);
        Console.WriteLine(request.Status);
        if ((request.Status == CorrespondenceStatus.Confirmed || request.Status == CorrespondenceStatus.Read) && currentStatus?.Status != CorrespondenceStatus.Published)
        {
            return Errors.CorrespondenceNotPublished;
        }

        if (currentStatus?.Status == request.Status)
        {
            return request.CorrespondenceId;
        }

        await _correspondenceRepository.UpdateCorrespondenceStatus(request.CorrespondenceId, request.Status, cancellationToken);
        return request.CorrespondenceId;
    }
}
