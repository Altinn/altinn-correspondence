using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeCorrespondenceCommand;

public class InitializeCorrespondenceCommandHandler : IHandler<InitializeCorrespondenceCommandRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    public InitializeCorrespondenceCommandHandler(ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository)
    {
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeCorrespondenceCommandRequest request, CancellationToken cancellationToken)
    {
        var statuses = new List<CorrespondenceStatusEntity>(){
            new CorrespondenceStatusEntity
            {
                Status = CorrespondenceStatus.Initialized,
                StatusChanged = DateTimeOffset.UtcNow
            }
        };
        request.correspondence.Statuses = statuses;

        var attachments = request.existingAttachments;
        if (request.newAttachments != null)
        {
            var attachmentIds = await _attachmentRepository.InitializeMultipleAttachments(request.newAttachments, cancellationToken);
            attachments = attachmentIds.Concat(request.existingAttachments).ToList();
        }
        //request.correspondence.Content.Attachments = attachments;

        var correspondenceId = await _correspondenceRepository.InitializeCorrespondence(request.correspondence, cancellationToken);
        return correspondenceId;
    }
}
