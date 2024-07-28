using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Web;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Markdig;
using OneOf;
using ReverseMarkdown;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class InitializeCorrespondenceHandler : IHandler<InitializeCorrespondenceRequest, InitializeCorrespondenceResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IEventBus _eventBus;
    IBackgroundJobClient _backgroundJobClient;

    public InitializeCorrespondenceHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository, IEventBus eventBus, IBackgroundJobClient backgroundJobClient)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<OneOf<InitializeCorrespondenceResponse, Error>> Process(InitializeCorrespondenceRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.Correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Send }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (!ValidatePlainText(request.Correspondence.Content?.MessageTitle))
        {
            return Errors.MessageTitleIsNotPlainText;
        }
        if (!ValidateMarkdown(request.Correspondence.Content?.MessageBody))
        {
            return Errors.MessageBodyIsNotMarkdown;
        }
        if (!ValidateMarkdown(request.Correspondence.Content?.MessageSummary))
        {
            return Errors.MessageSummaryIsNotMarkdown;
        }
        var attachments = request.Correspondence.Content?.Attachments;
        if (attachments != null)
        {
            foreach (var attachment in attachments)
            {
                attachment.Attachment = await ProcessAttachment(attachment, request.Correspondence, cancellationToken);
            }

        }

        var status = GetInitializeCorrespondenceStatus(request.Correspondence);
        var statuses = new List<CorrespondenceStatusEntity>(){
            new CorrespondenceStatusEntity
            {
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = status.ToString()
            }
        };
        request.Correspondence.Statuses = statuses;
        request.Correspondence.Notifications = ProcessNotifications(request.Correspondence.Notifications, cancellationToken);
        var correspondence = await _correspondenceRepository.InitializeCorrespondence(request.Correspondence, cancellationToken);
        _backgroundJobClient.Schedule<PublishCorrespondenceService>((service) => service.Publish(correspondence.Id, cancellationToken), request.Correspondence.VisibleFrom);
        await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, request.Correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", request.Correspondence.Sender, cancellationToken);
        return new InitializeCorrespondenceResponse()
        {
            CorrespondenceId = correspondence.Id,
            AttachmentIds = correspondence.Content?.Attachments.Select(a => a.AttachmentId).ToList() ?? new List<Guid>()
        };
    }

    public CorrespondenceStatus GetInitializeCorrespondenceStatus(CorrespondenceEntity correspondence)
    {
        var status = CorrespondenceStatus.Initialized;
        if (correspondence.Content != null && correspondence.Content.Attachments.All(c => c.Attachment?.Statuses != null && c.Attachment.Statuses.All(s => s.Status == AttachmentStatus.Published)))
        {
            status = correspondence.VisibleFrom < DateTime.UtcNow ? CorrespondenceStatus.Published : CorrespondenceStatus.ReadyForPublish;
        }
        return status;
    }

    public async Task<AttachmentEntity> ProcessAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        AttachmentEntity? attachment = null;
        if (!String.IsNullOrEmpty(correspondenceAttachment.DataLocationUrl))
        {
            var existingAttachment = await _attachmentRepository.GetAttachmentByUrl(correspondenceAttachment.DataLocationUrl, cancellationToken);
            if (existingAttachment != null)
            {
                attachment = existingAttachment;
            }
        }
        if (attachment == null)
        {

            var status = new List<AttachmentStatusEntity>(){
                    new AttachmentStatusEntity
                    {
                        Status = AttachmentStatus.Initialized,
                        StatusChanged = DateTimeOffset.UtcNow,
                        StatusText = AttachmentStatus.Initialized.ToString()
                    }
                };
            attachment = new AttachmentEntity
            {
                ResourceId = correspondence.ResourceId,
                SendersReference = correspondenceAttachment.SendersReference,
                RestrictionName = correspondenceAttachment.RestrictionName,
                ExpirationTime = correspondenceAttachment.ExpirationTime,
                DataType = correspondenceAttachment.DataType,
                DataLocationUrl = correspondenceAttachment.DataLocationUrl,
                Statuses = status,
                Created = DateTimeOffset.UtcNow
            };
        }
        return attachment;
    }

    private List<CorrespondenceNotificationEntity> ProcessNotifications(List<CorrespondenceNotificationEntity>? notifications, CancellationToken cancellationToken)
    {
        if (notifications == null) return new List<CorrespondenceNotificationEntity>();

        foreach (var notification in notifications)
        {
            notification.Statuses = new List<CorrespondenceNotificationStatusEntity>(){
                new CorrespondenceNotificationStatusEntity
                {
                     Status = "Initialized", //TODO create enums for notications?
                     StatusChanged = DateTimeOffset.UtcNow,
                     StatusText = "Initialized"
                }
              };
        }
        return notifications;
    }

    private bool ValidatePlainText(string text)
    {
        var converter = new ReverseMarkdown.Converter();
        var markdown = converter.Convert(text);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var plaintext = Markdown.ToPlainText(markdown, pipeline);
        return plaintext.Trim() == text.Trim();
    }

    private bool ValidateMarkdown(string markdown)
    {
        var config = new ReverseMarkdown.Config
        {
            CleanupUnnecessarySpaces = false,
            PassThroughTags = new String[] { "br" },
        };
        var converter = new ReverseMarkdown.Converter(config);
        // change all codeblocks to <code> to keep html content in codeblocks
        var markdownWithCodeBlocks = ReplaceMarkdownCodeWithHtmlCode(markdown);
        string result = converter.Convert(markdownWithCodeBlocks);

        // needs to decode the text twice as some encoded characters contains encoded characters, such as emdash &#8212;
        var text = WebUtility.HtmlDecode(WebUtility.HtmlDecode(markdown));
        result = WebUtility.HtmlDecode(WebUtility.HtmlDecode(result));

        //As reversemarkdown makes all code blocks to ` we need to replace ``` with ` and `` with ` to compare the strings
        return ReplaceWhitespaceAndEscapeCharacters(text.Replace("```", "`").Replace("``", "`")) == ReplaceWhitespaceAndEscapeCharacters(result.Replace("```", "`").Replace("``", "`"));
    }

    private string ReplaceWhitespaceAndEscapeCharacters(string text)
    {
        return Regex.Replace(text, @"\s+", "").Replace("\\", "").ToLower();
    }

    private string ReplaceMarkdownCodeWithHtmlCode(string text)
    {
        var codeTagsContent = new List<List<string>>();
        var validCodeTagDelimiters = new List<string> { "```", "``", "`" };
        var newText = text;
        var i = 0;
        foreach (var delimiter in validCodeTagDelimiters)
        {
            var counter = 0;
            var markdownWithCodeBlocks = newText.Split(delimiter);
            var tagList = new List<string>();
            newText = "";
            for (var j = 0; j < markdownWithCodeBlocks.Length; j++)
            {
                if (j % 2 == 1)
                {
                    newText += "<---CODE" + i + counter + "--->";
                    tagList.Add(markdownWithCodeBlocks[j].Replace("<", "&lt;").Replace(">", "&gt;"));
                    counter++;
                }
                else newText += markdownWithCodeBlocks[j];
            }
            codeTagsContent.Add(tagList);
            i++;
        }
        for (var j = 0; j < 3; j++)
        {
            var counter = 0;
            foreach (var t in codeTagsContent[j])
            {
                newText = newText.Replace("<---CODE" + j + counter + "--->", "<code>" + t + "</code>");
                counter++;
            }
        }
        return newText;
    }
}
