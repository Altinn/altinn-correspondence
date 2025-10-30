using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{

    internal static class CreateDialogTransmissionMapper
    {
        internal static CreateTransmissionRequest CreateDialogTransmission(CorrespondenceEntity correspondence, string baseUrl, bool includeActivities = false, ILogger? logger = null)
        {
            var dialogId = Guid.CreateVersion7().ToString(); // Dialogporten requires time-stamped GUIDs
            DateTimeOffset? dueAt = correspondence.DueDateTime != default ? correspondence.DueDateTime : null;

            // The problem of DueAt being in the past should only occur for migrated data, as such we are checking includeActivities flag first, since this is only set when making migrated correspondences available.
            if (includeActivities && dueAt.HasValue && dueAt < DateTimeOffset.Now)
            {
                dueAt = null;
            }

            return new CreateTransmissionRequest
            {
                Id = dialogId,
                CreatedAt = correspondence.Created,
                AuthorizationAttribute = UrnConstants.Resource + ":" + correspondence.ResourceId,
                IsAuthorized = true,
                ExternalReference = correspondence.SendersReference,
                RelatedTransmissionId = null,
                Type = TransmissionType.Information,
                Sender = CreateTransmissionSender(correspondence),
                Content = CreateTransmissionContent(correspondence, baseUrl),
                Attachments = GetAttachmentsForCorrespondence(baseUrl, correspondence),
            };
        }

        private static TransmissionContent CreateTransmissionContent(CorrespondenceEntity correspondence, string baseUrl) => new()
        {
            Title = new TransmissionTitle()
            {
                MediaType = "text/plain",
                Value = new List<TransmissionValue>
                {
                    new TransmissionValue
                    {
                        Value = correspondence.Content!.MessageTitle ?? "", // A required field, DP will throw validation error if empty, but should not be possible to reach this point with empty title
                        LanguageCode = correspondence.Content.Language,
                    }
                }
            },
            Summary = string.IsNullOrWhiteSpace(correspondence.Content.MessageSummary) ? null : new TransmissionSummary()
            {
                MediaType = "text/plain",
                Value = new List<TransmissionValue>{
                    new TransmissionValue
                    {
                        Value = TextValidation.StripSummaryForHtmlAndMarkdown(correspondence.Content.MessageSummary ?? ""),
                        LanguageCode = correspondence.Content.Language
                    }
                }
            },
            ContentReference = new TransmissionContentReference
            {
                MediaType = "application/vnd.dialogporten.frontchannelembed-url;type=text/markdown",
                Value = new List<TransmissionValue>
                {
                    new TransmissionValue
                    {
                        Value = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/content",
                        LanguageCode = correspondence.Content.Language
                    }
            }
            }
        };

        private static List<TransmissionAttachment> GetAttachmentsForCorrespondence(string baseUrl, CorrespondenceEntity correspondence)
        {
            var baseTimestamp = DateTimeOffset.UtcNow;
            return correspondence.Content?.Attachments.Select((attachment, index) => new TransmissionAttachment
            {
                Id = Guid.CreateVersion7(baseTimestamp.AddMilliseconds(index)).ToString(),
                DisplayName = new List<TransmissionDisplayName>
                {
                    new TransmissionDisplayName
                    {
                        LanguageCode = correspondence.Content.Language,
                        Value = attachment.Attachment.DisplayName ?? attachment.Attachment.FileName
                    }
                },
                Urls = new List<TransmissionUrl>
                {
                    new TransmissionUrl
                    {
                        ConsumerType = "Gui",
                        MediaType = "application/vnd.dialogporten.frontchannelembed-url;type=text/markdown",
                        Url = GetDownloadAttachmentEndpoint(baseUrl, correspondence.Id, attachment.AttachmentId)
                    }
                }
            }).ToList() ?? new List<TransmissionAttachment>();
        }
        private static string GetDownloadAttachmentEndpoint(string baseUrl, Guid correspondenceId, Guid attachmentId)
        {
            return $"{baseUrl.Trim('/')}/correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentId}/download";
        }

        private static TransmissionSender CreateTransmissionSender(CorrespondenceEntity correspondence)
        {
            return new TransmissionSender
            {
                ActorId = correspondence.GetRecipientUrn(),
                ActorType = "PartyRepresentative",

            };
        }
    }
}