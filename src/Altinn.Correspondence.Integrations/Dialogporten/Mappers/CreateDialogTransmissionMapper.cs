using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Microsoft.Extensions.Logging;


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
                        Value = TruncateTitleForDialogporten(correspondence.Content!.MessageTitle ?? "Ingen tittel"),
                        LanguageCode = "nb",
                    }
                }
            },
            Summary = string.IsNullOrWhiteSpace(correspondence.Content.MessageSummary) ? null : new TransmissionSummary()
            {
                MediaType = "text/plain",
                Value = new List<TransmissionValue>
                {
                    new TransmissionValue
                    {
                        Value = correspondence.Content.MessageSummary,
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
                        Value = $"{baseUrl}/correspondence/{correspondence.Id}",
                        LanguageCode = "nb"
                    }
            }
            }
        };


        /// <summary>
        /// Truncates titles longer than 255 characters to 252 characters and adds "..." to fit within Dialogporten's 255 character limit.
        /// Titles 255 characters or shorter are sent as-is to Dialogporten.
        /// This serves as a safety net for existing correspondence with long titles that failed Dialog Porten creation,
        /// allowing them to retry successfully.
        /// </summary>
        /// <param name="title">The original title</param>
        /// <returns>The title truncated to fit Dialogporten's requirements</returns>
        private static string TruncateTitleForDialogporten(string title)
        {
            if (string.IsNullOrEmpty(title))
                return title;

            // Dialogporten has a 255 character limit, so we truncate to 252 and add "..." only for titles > 255 chars
            return title.Length <= 255 ? title : title.Substring(0, 252) + "...";
        }


        private static List<TransmissionAttachment> GetAttachmentsForCorrespondence(string baseUrl, CorrespondenceEntity correspondence)
        {
            return correspondence.Content?.Attachments.Select((attachment, index) => new TransmissionAttachment
            {
                Id = Guid.CreateVersion7().ToString(),
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