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
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IEventBus _eventBus;
    IBackgroundJobClient _backgroundJobClient;
    public InitializeCorrespondenceHandler(ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository, IEventBus eventBus, IBackgroundJobClient backgroundJobClient)
    {
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<OneOf<InitializeCorrespondenceResponse, Error>> Process(InitializeCorrespondenceRequest request, CancellationToken cancellationToken)
    {
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
                attachment.Attachment = await ProcessAttachment(attachment, cancellationToken);
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
        await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, null, correspondence.Id.ToString(), "correspondence", null, cancellationToken);
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

    public async Task<AttachmentEntity> ProcessAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, CancellationToken cancellationToken)
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
        markdownWithCodeBlocks = EscapeHtmlBetweenCodeTags(markdownWithCodeBlocks);
        string result = converter.Convert(markdownWithCodeBlocks);
        result = UnescapeHtmlInCodeBlock(result);

        markdown = WebUtility.HtmlDecode(markdown);
        result = WebUtility.HtmlDecode(result);
        //As reversemarkdown makes all code blocks to ` we need to replace ``` with ` and `` with ` to compare the strings
        return ReplaceWhitespaceAndEscapeCharacters(markdown.Replace("```", "`").Replace("``", "`")) == ReplaceWhitespaceAndEscapeCharacters(result);
    }

    private string ReplaceWhitespaceAndEscapeCharacters(string text)
    {
        return Regex.Replace(text, @"\s+", "").Replace("\\", "").ToLower();
    }

    private string ReplaceMarkdownCodeWithHtmlCode(string text)
    {
        var markdownWithCodeBlocks = Regex.Replace(text, @"([^```]*```[^```]*)```", "$1</code>"); // add code to keep code blocks in markdown
        markdownWithCodeBlocks = markdownWithCodeBlocks.Replace("```", "<code>");
        markdownWithCodeBlocks = Regex.Replace(markdownWithCodeBlocks, @"([^``]*``[^``]*)``", "$1</code>");
        markdownWithCodeBlocks = markdownWithCodeBlocks.Replace("``", "<code>");
        markdownWithCodeBlocks = Regex.Replace(markdownWithCodeBlocks, @"([^`]*`[^`]*)`", "$1</code>");
        markdownWithCodeBlocks = markdownWithCodeBlocks.Replace("`", "<code>");
        return markdownWithCodeBlocks;
    }

    private string EscapeHtmlBetweenCodeTags(string text)
    {
        return Regex.Replace(text, @"(?s)(?<=<code>)(.*?)(?=</code>)", m => m.Value.Replace("<", "&lt;").Replace(">", "&gt;"));
    }
    private string UnescapeHtmlInCodeBlock(string text)
    {
        return Regex.Replace(text, @"`*>[\s\S]*?`", m => m.Value.Replace("&lt;", "<").Replace("&gt;", ">"));
    }

}
