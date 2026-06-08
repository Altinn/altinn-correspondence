using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Integrations.Dialogporten.Models;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class DialogPatchRequestBuilder
    {
        private List<object> _PatchDialogRequest = new List<object>();

        public List<object> Build()
        {
            return _PatchDialogRequest;
        }

        internal DialogPatchRequestBuilder WithRemoveGuiActionOperation(int guiActionToRemoveIndex)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "remove",
                    path = $"/guiActions/{guiActionToRemoveIndex}"
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithRemoveApiActionOperation(int apiActionToRemoveIndex)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "remove",
                    path = $"/apiActions/{apiActionToRemoveIndex}"
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithReplaceStatusOperation(string newStatus)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "replace",
                    path = "/status",
                    value = newStatus
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithRemoveExpiresAtOperation()
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "remove",
                    path = "/expiresAt"
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithReplaceSummaryOperation(string newSummary)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "replace",
                    path = "/content/summary/value/0/value",
                    value = newSummary
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithRemoveAttachmentsOperation()
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "remove",
                    path = "/attachments"
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithAddDownloadAllAttachmentsOperation(string baseUrl, CorrespondenceEntity correspondence, List<Attachment> attachments)
        {
            var baseTimestamp = DateTimeOffset.UtcNow;
            var dialogAttachments = attachments;
            var downloadAllAttachment = 
                new Attachment
                    {
                        Id = Guid.CreateVersion7(DateTimeOffset.UnixEpoch).ToString(),
                        DisplayName = new List<DisplayName>
                    {
                        new DisplayName { LanguageCode = "nb", Value = "Alle vedlegg" },
                        new DisplayName { LanguageCode = "nn", Value = "Alle vedlegg" },
                        new DisplayName { LanguageCode = "en", Value = "All attachments" }
                    },
                        Urls = new List<DialogUrl>
                    {
                        new DialogUrl
                        {
                            ConsumerType = "Gui",
                            MediaType = "application/zip",
                            Url = GetDownloadAllAttachmentsEndpoint(baseUrl, correspondence.Id)
                        }
                    }
                    };
            dialogAttachments.Insert(0, downloadAllAttachment);
                _PatchDialogRequest.Add(
                    new
                    {
                        op = "add",
                        path = "/attachments",
                        value = dialogAttachments.Select(a => new
                        {
                            id = a.Id,
                            displayName = a.DisplayName,
                            urls = a.Urls?.Select(u => new
                            {
                                consumerType = u.ConsumerType,
                                mediaType = u.MediaType,
                                url = u.Url
                            })
                        })
                    }
                );
            return this;
        }


        private static string GetDownloadAllAttachmentsEndpoint(string baseUrl, Guid correspondenceId)
        {
            return $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondenceId}/attachments/downloadall";
        }
    }
}