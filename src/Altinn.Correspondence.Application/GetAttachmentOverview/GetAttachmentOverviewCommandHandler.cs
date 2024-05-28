using Altinn.Correspondence.Application.GetAttachmentOverviewCommand;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentOverviewCommand;

public class GetAttachmentOverviewCommandHandler : IHandler<Guid, GetAttachmentOverviewCommandResponse>
{
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    public GetAttachmentOverviewCommandHandler(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository)
    {
        _attachmentStatusRepository = attachmentStatusRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<GetAttachmentOverviewCommandResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, false, cancellationToken);
        var attachmentStatus = await _attachmentStatusRepository.GetLatestStatusByAttachmentId(attachmentId, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }

        var response = new GetAttachmentOverviewCommandResponse
        {
            AttachmentId = attachment.Id,
            DataLocationUrl = attachment.DataLocationUrl,
            Name = attachment.FileName,
            Status = attachmentStatus?.Status,
            StatusText = attachmentStatus?.StatusText,
            StatusChanged = attachmentStatus?.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            IntendedPresentation = attachment.IntendedPresentation,
            SendersReference = attachment.SendersReference
        };
        return response;
    }


}
