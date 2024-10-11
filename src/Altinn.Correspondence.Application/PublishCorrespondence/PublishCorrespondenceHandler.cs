using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Logging;
using OneOf;
using Microsoft.Extensions.Hosting;
using Altinn.Correspondence.Application.CancelNotification;
using Hangfire;

namespace Altinn.Correspondence.Application.PublishCorrespondence;

public class PublishCorrespondenceHandler : IHandler<Guid, Task>
{
    private readonly ILogger<PublishCorrespondenceHandler> _logger;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IEventBus _eventBus;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public PublishCorrespondenceHandler(
        ILogger<PublishCorrespondenceHandler> logger,
        ICorrespondenceRepository correspondenceRepository,
        ICorrespondenceStatusRepository correspondenceStatusRepository,
        IEventBus eventBus,
        IHostEnvironment hostEnvironment,
        IBackgroundJobClient backgroundJobClient)
    {
        _logger = logger;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _eventBus = eventBus;
        _hostEnvironment = hostEnvironment;
        _backgroundJobClient = backgroundJobClient;
    }


    public async Task<OneOf<Task, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publish correspondence {correspondenceId}", correspondenceId);
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        var errorMessage = "";
        if (correspondence == null)
        {
            errorMessage = "Correspondence " + correspondenceId + " not found when publishing";
        }
        else if (_hostEnvironment.IsDevelopment() && correspondence.GetLatestStatus()?.Status == CorrespondenceStatus.Published)
        {
            return Task.CompletedTask;
        }
        else if (correspondence.GetLatestStatus()?.Status != CorrespondenceStatus.ReadyForPublish)
        {
            errorMessage = $"Correspondence {correspondenceId} not ready for publish";
        }
        else if (correspondence.Content == null || correspondence.Content.Attachments.Any(a => a.Attachment?.GetLatestStatus()?.Status != AttachmentStatus.Published))
        {
            errorMessage = $"Correspondence {correspondenceId} has attachments not published";
        }
        else if (correspondence.VisibleFrom > DateTimeOffset.UtcNow)
        {
            errorMessage = $"Correspondence {correspondenceId} not visible yet";
        }
        CorrespondenceStatusEntity status;
        AltinnEventType eventType = AltinnEventType.CorrespondencePublished;
        if (errorMessage.Length > 0)
        {
            _logger.LogError(errorMessage);
            status = new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Failed,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = errorMessage
            };
            eventType = AltinnEventType.CorrespondencePublishFailed;
            foreach (var notification in correspondence.Notifications)
            {
                _backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondence.Notifications, cancellationToken));
            }
        }
        else
        {
            status = new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.Published,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.Published.ToString()
            };
            _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.ServiceOwner, Core.Dialogporten.Mappers.DialogportenTextType.CorrespondencePublished));
        }

        await _correspondenceStatusRepository.AddCorrespondenceStatus(status, cancellationToken);
        await _eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        if (status.Status == CorrespondenceStatus.Published) await _eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
        return Task.CompletedTask;
    }
}