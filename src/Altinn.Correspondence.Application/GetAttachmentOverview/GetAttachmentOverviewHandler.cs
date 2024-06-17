using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetAttachmentOverview;

public class GetAttachmentOverviewHandler : IHandler<Guid, GetAttachmentOverviewResponse>
{
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IAttachmentRepository _attachmentRepository;

    public GetAttachmentOverviewHandler(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository)
    {
        _attachmentStatusRepository = attachmentStatusRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<GetAttachmentOverviewResponse, Error>> Process(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, false, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var attachmentStatus = await _attachmentStatusRepository.GetLatestStatusByAttachmentId(attachmentId, cancellationToken);

        var response = new GetAttachmentOverviewResponse
        {
            AttachmentId = attachment.Id,
            DataLocationUrl = attachment.DataLocationUrl,
            Name = attachment.FileName,
            Status = attachmentStatus.Status,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            IntendedPresentation = attachment.IntendedPresentation,
            SendersReference = attachment.SendersReference
        };
        return response;
    }
}
