using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal static class CreateDialogRequestMapper
    {
        internal static CreateDialogRequest CreateCorrespondenceDialog(CorrespondenceEntity correspondence, string organizationNo, string dialogId)
        {
            return new CreateDialogRequest
            {
                Id = dialogId,
                ServiceResource = "urn:altinn:resource:" + correspondence.ResourceId,
                Party = "urn:altinn:organization:identifier-no:" + organizationNo,
                CreatedAt = correspondence.Created,
                RequestedPublishTime = correspondence.RequestedPublishTime < DateTimeOffset.UtcNow.AddMinutes(1) ? DateTimeOffset.UtcNow.AddMinutes(1) : correspondence.RequestedPublishTime,
                Process = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == Core.Models.Enums.ReferenceType.DialogportenProcessId)?.ReferenceValue,
                ExpiresAt = correspondence.AllowSystemDeleteAfter,
                DueAt = correspondence.DueDateTime != default ? correspondence.DueDateTime : null,
                Status = "New",
                ExternalReference = correspondence.SendersReference,
                Content = CreateCorrespondenceContent(correspondence),
                SearchTags = GetSearchTagsForCorrespondence(correspondence),
                ApiActions = GetApiActionsForCorrespondence(correspondence),
                GuiActions = GetGuiActionsForCorrespondence(correspondence),
                Attachments = GetAttachmentsForCorrespondence(correspondence),
                Activities = GetActivitiesForCorrespondence(correspondence)
            };
        }

        private static Content CreateCorrespondenceContent(CorrespondenceEntity correspondence) => new()
        {
            Title = new Title()
            {
                LanguageCode = "nb",
                MediaType = "text/plain",
                Value = [
                    new DialogValue()
                    {
                        LanguageCode = correspondence.Content.Language,
                        Value = correspondence.Content.MessageTitle
                    }
                ]
            },
            Summary = new Summary()
            {
                MediaType = "text/plain",
                Value = [
                    new DialogValue()
                    {
                        LanguageCode = correspondence.Content.Language,
                        Value = correspondence.Content.MessageSummary
                    }
                ],
            },
            SenderName = new SenderName()
            {
                MediaType = "text/plain",
                Value = [
                    new DialogValue()
                    {
                        LanguageCode = correspondence.Content.Language,
                        Value = correspondence.MessageSender ?? correspondence.Sender
                    }
                ]
            }
        };

        private static List<SearchTag> GetSearchTagsForCorrespondence(CorrespondenceEntity correspondence)
        {
            // TODO: Implement search tags
            return new List<SearchTag>();
        }

        private static List<Activity> GetActivitiesForCorrespondence(CorrespondenceEntity correspondence)
        {
            // TODO: Implement activities
            return new List<Activity>();
        }

        private static string GetDownloadAttachmentEndpoint(Guid correspondenceId, Guid attachmentId)
        {
            // TODO: Implement API endpoint discovery
            return $"https.//platform.tt02.altinn.no/correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentId}/download";
        }   
        private static List<ApiAction> GetApiActionsForCorrespondence(CorrespondenceEntity correspondence)
        {
            // TODO: Implement API actions
            return new List<ApiAction>();
        }

        private static List<GuiAction> GetGuiActionsForCorrespondence(CorrespondenceEntity correspondence)
        {
            // TODO: Implement GUI actions
            return new List<GuiAction>();
        }

        private static List<Attachment> GetAttachmentsForCorrespondence(CorrespondenceEntity correspondence)
        {
            return correspondence.Content?.Attachments.Select(attachment => new Attachment
            {
                DisplayName = new List<DisplayName>
                {
                    new DisplayName
                    {
                        LanguageCode = correspondence.Content.Language,
                        Value = attachment.Attachment.Name
                    }
                },
                Urls = new List<DialogUrl>
                {
                    new DialogUrl
                    {
                        ConsumerType = "Api",
                        MediaType = "application/octet-stream",
                        Url = GetDownloadAttachmentEndpoint(correspondence.Id, attachment.Id)
                    }
                }
            }).ToList() ?? new List<Attachment>();
        }
    }
}
