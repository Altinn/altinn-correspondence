using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeMultipleCorrespondences;

public class InitializeMultipleCorrespondencesHandler : IHandler<InitializeMultipleCorrespondencesRequest, InitializeMultipleCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IEventBus _eventBus;
    private readonly InitializeCorrespondenceHelper _initializeCorrespondenceHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public InitializeMultipleCorrespondencesHandler(InitializeCorrespondenceHelper initializeCorrespondenceHelper, IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, IEventBus eventBus, IBackgroundJobClient backgroundJobClient)
    {
        _initializeCorrespondenceHelper = initializeCorrespondenceHelper;
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<OneOf<InitializeMultipleCorrespondencesResponse, Error>> Process(InitializeMultipleCorrespondencesRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.Correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Send }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (request.isUploadRequest && request.Attachments.Count == 0)
        {
            return Errors.NoAttachments;
        }
        if (request.Recipients.Count != request.Recipients.Distinct().Count())
        {
            return Errors.DuplicateRecipients;
        }
        var contentError = _initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.Correspondence.Content);
        if (contentError != null)
        {
            return contentError;
        }

        var attachmentError = _initializeCorrespondenceHelper.ValidateAttachmentFiles(request.Attachments, request.Correspondence.Content!.Attachments, true);
        if (attachmentError != null) return attachmentError;
        var attachments = new List<AttachmentEntity>();
        if (request.Correspondence.Content!.Attachments.Count() > 0)
        {
            foreach (var attachment in request.Correspondence.Content!.Attachments)
            {
                var a = await _initializeCorrespondenceHelper.ProcessAttachment(attachment, cancellationToken);
                attachments.Add(a);
            }
        }
        if (request.Attachments.Count > 0)
        {
            var uploadError = await _initializeCorrespondenceHelper.UploadAttachments(attachments, request.Attachments, cancellationToken);
            if (uploadError != null)
            {
                return uploadError;
            }
        }
        var status = _initializeCorrespondenceHelper.GetInitializeCorrespondenceStatus(request.Correspondence);
        var correspondences = new List<CorrespondenceEntity>();
        foreach (var recipient in request.Recipients)
        {
            var correspondence = new CorrespondenceEntity
            {
                ResourceId = request.Correspondence.ResourceId,
                Recipient = recipient,
                Sender = request.Correspondence.Sender,
                SendersReference = request.Correspondence.SendersReference,
                MessageSender = request.Correspondence.MessageSender,
                Content = new CorrespondenceContentEntity
                {
                    Attachments = attachments.Select(a => new CorrespondenceAttachmentEntity
                    {
                        Attachment = a,
                        Created = DateTimeOffset.UtcNow,

                    }).ToList(),
                    Language = request.Correspondence.Content.Language,
                    MessageBody = request.Correspondence.Content.MessageBody,
                    MessageSummary = request.Correspondence.Content.MessageSummary,
                    MessageTitle = request.Correspondence.Content.MessageTitle,
                },
                VisibleFrom = request.Correspondence.VisibleFrom,
                AllowSystemDeleteAfter = request.Correspondence.AllowSystemDeleteAfter,
                DueDateTime = request.Correspondence.DueDateTime,
                PropertyList = request.Correspondence.PropertyList.ToDictionary(x => x.Key, x => x.Value),
                ReplyOptions = request.Correspondence.ReplyOptions,
                IsReservable = request.Correspondence.IsReservable,
                Notifications = _initializeCorrespondenceHelper.ProcessNotifications(request.Correspondence.Notifications, cancellationToken),
                Statuses = new List<CorrespondenceStatusEntity>(){
                    new CorrespondenceStatusEntity
                    {
                        Status = status,
                        StatusChanged = DateTimeOffset.UtcNow,
                        StatusText = status.ToString()
                    }
                },
                Created = request.Correspondence.Created,
                ExternalReferences = request.Correspondence.ExternalReferences,
            };
            correspondences.Add(correspondence);
        }
        correspondences = await _correspondenceRepository.CreateMultipleCorrespondences(correspondences, cancellationToken);
        foreach (var correspondence in correspondences)
        {
            _backgroundJobClient.Schedule<PublishCorrespondenceService>((service) => service.Publish(correspondence.Id, cancellationToken), correspondence.VisibleFrom);
            await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        }
        return new InitializeMultipleCorrespondencesResponse()
        {
            CorrespondenceIds = correspondences.Select(c => c.Id).ToList(),
        };
    }
}
