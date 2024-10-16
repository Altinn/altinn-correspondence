using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    internal static class CreateDialogRequestMapper
    {
        internal static CreateDialogRequest CreateCorrespondenceDialog(CorrespondenceEntity correspondence, string baseUrl)
        {
            var dialogId = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(); // Dialogporten requires time-stamped GUIDs, not supported natively until .NET 9.0
            return new CreateDialogRequest
            {
                Id = dialogId,
                ServiceResource = "urn:altinn:resource:" + correspondence.ResourceId,
                Party = correspondence.GetRecipientUrn(),
                CreatedAt = correspondence.Created,
                VisibleFrom = correspondence.RequestedPublishTime < DateTime.UtcNow.AddMinutes(1) ? DateTime.UtcNow.AddMinutes(1) : correspondence.RequestedPublishTime,
                Process = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenProcessId)?.ReferenceValue,
                ExpiresAt = correspondence.AllowSystemDeleteAfter,
                DueAt = correspondence.DueDateTime != default ? correspondence.DueDateTime : null,
                Status = "New",
                ExternalReference = correspondence.SendersReference,
                Content = CreateCorrespondenceContent(correspondence, baseUrl),
                SearchTags = GetSearchTagsForCorrespondence(correspondence),
                ApiActions = GetApiActionsForCorrespondence(baseUrl, correspondence),
                GuiActions = GetGuiActionsForCorrespondence(baseUrl, correspondence),
                Attachments = GetAttachmentsForCorrespondence(baseUrl, correspondence),
                Activities = GetActivitiesForCorrespondence(correspondence)
            };
        }

        private static Content CreateCorrespondenceContent(CorrespondenceEntity correspondence, string baseUrl) => new()
        {
            Title = new ContentValue()
            {
                MediaType = "text/plain",
                Value = new List<DialogValue> {
                    new DialogValue()
                    {
                        Value = correspondence.Content!.MessageTitle ?? "",
                        LanguageCode = correspondence.Content.Language
                    }
                }
            },
            Summary = new ContentValue()
            {
                MediaType = "text/plain",
                Value = new List<DialogValue> {
                    new DialogValue()
                    {
                        Value = correspondence.Content.MessageSummary,
                        LanguageCode = correspondence.Content.Language
                    }
                }
            },
            SenderName = new ContentValue()
            {
                MediaType = "text/plain",
                Value = new List<DialogValue> {
                    new DialogValue()
                    {
                        Value = correspondence.MessageSender ?? correspondence.Sender,
                        LanguageCode = correspondence.Content.Language
                    }
                }
            },
            MainContentReference = new ContentValue()
            {
                MediaType = "application/vnd.dialogporten.frontchannelembed+json;type=markdown",
                Value = [
                    new DialogValue()
                    {
                        LanguageCode = correspondence.Content.Language,
                        Value = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/content"
                    }
                ]
            }
        };

        private static List<SearchTag> GetSearchTagsForCorrespondence(CorrespondenceEntity correspondence)
        {
            var list = new List<SearchTag>();
            list = AddSearchTagIfValid(list, correspondence.SendersReference);
            list = AddSearchTagIfValid(list, correspondence.Sender);
            list = AddSearchTagIfValid(list, correspondence.ResourceId);
            list = AddSearchTagIfValid(list, correspondence.MessageSender);
            foreach (var reference in correspondence.ExternalReferences)
            {
                list = AddSearchTagIfValid(list, reference.ReferenceType.ToString());
                list = AddSearchTagIfValid(list, reference.ReferenceValue.ToString());
            }
            list = list.DistinctBy(tag => tag.Value).ToList(); // Remove duplicates
            return list;
        }

        private static List<SearchTag> AddSearchTagIfValid(List<SearchTag> list, string? searchTag)
        {
            if (string.IsNullOrWhiteSpace(searchTag) || searchTag.Length < 3)
            {
                return list;
            }
            list.Add(
                new SearchTag()
                {
                    Value = searchTag
                }
            );
            return list;
        }

        private static List<Activity> GetActivitiesForCorrespondence(CorrespondenceEntity correspondence)
        {
            return new List<Activity>();
        }

        private static string GetDownloadAttachmentEndpoint(string baseUrl, Guid correspondenceId, Guid attachmentId)
        {
            return $"{baseUrl.Trim('/')}/correspondence/api/v1/correspondence/{correspondenceId}/attachment/{attachmentId}/download";
        }
        private static List<ApiAction> GetApiActionsForCorrespondence(string baseUrl, CorrespondenceEntity correspondence)
        {
            var apiActions = new List<ApiAction>
            {
                new ApiAction()
                {
                    Action = "write",
                    Endpoints = new List<Endpoint>()
                    {
                        new Endpoint()
                        {
                            HttpMethod = "POST",
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/confirm"
                        }
                    }
                },
                new ApiAction()
                {
                    Action = "write",
                    Endpoints = new List<Endpoint>()
                    {
                        new Endpoint()
                        {
                            HttpMethod = "POST",
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/archive"
                        }
                    }
                },
                new ApiAction()
                {
                    Action = "write",
                    Endpoints = new List<Endpoint>()
                    {
                        new Endpoint()
                        {
                            HttpMethod = "DELETE",
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/purge"
                        }
                    }
                },
                new ApiAction()
                {
                    Action = "write",
                    Endpoints = new List<Endpoint>()
                    {
                        new Endpoint()
                        {
                            HttpMethod = "POST",
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/markasread"
                        }
                    }
                }
            };
            foreach (var attachment in correspondence.Content?.Attachments)
            {
                apiActions.Add(new ApiAction()
                {
                    Action = "read",
                    Endpoints = new List<Endpoint>()
                    {
                        new Endpoint()
                        {
                            HttpMethod = "GET",
                            Url = GetDownloadAttachmentEndpoint(baseUrl, correspondence.Id, attachment.AttachmentId)
                        }
                    }
                });
            }
            return apiActions;
        }

        private static List<GuiAction> GetGuiActionsForCorrespondence(string baseUrl, CorrespondenceEntity correspondence)
        {
            var guiActions = new List<GuiAction>
            {
                new GuiAction()
                {
                    Title = new List<Title>()
                    {
                        new Title()
                        {
                            LanguageCode = "nb",
                            MediaType = "text/plain",
                            Value = "Bekreft"
                        },
                        new Title()
                        {
                            LanguageCode = "nn",
                            MediaType = "text/plain",
                            Value = "Bekreft"
                        },
                        new Title()
                        {
                            LanguageCode = "en",
                            MediaType = "text/plain",
                            Value = "Confirm"
                        },
                    },
                    Action = "write",
                    Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/confirm",
                    HttpMethod = "POST",
                    Priority = "Primary"
                },
                new GuiAction()
                {
                    Title = new List<Title>()
                    {
                        new Title()
                        {
                            LanguageCode = "nb",
                            MediaType = "text/plain",
                            Value = "Arkiver"
                        },
                        new Title()
                        {
                            LanguageCode = "nn",
                            MediaType = "text/plain",
                            Value = "Arkiver"
                        },
                        new Title()
                        {
                            LanguageCode = "en",
                            MediaType = "text/plain",
                            Value = "Archive"
                        },
                    },
                    Action = "write",
                    Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/archive",
                    HttpMethod = "POST",
                    Priority = "Secondary"
                },
                new GuiAction()
                {
                    Title = new List<Title>()
                    {
                        new Title()
                        {
                            LanguageCode = "nb",
                            MediaType = "text/plain",
                            Value = "Marker som lest"
                        },
                        new Title()
                        {
                            LanguageCode = "nn",
                            MediaType = "text/plain",
                            Value = "Mark som lest"
                        },
                        new Title()
                        {
                            LanguageCode = "en",
                            MediaType = "text/plain",
                            Value = "Mark as read"
                        },
                    },
                    Action = "write",
                    Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/markasread",
                    HttpMethod = "POST",
                    Priority = "Tertiary"
                },
                new GuiAction()
                {
                    Title = new List<Title>()
                    {
                        new Title()
                        {
                            LanguageCode = "nb",
                            MediaType = "text/plain",
                            Value = "Slett"
                        },
                        new Title()
                        {
                            LanguageCode = "nn",
                            MediaType = "text/plain",
                            Value = "Slett"
                        },
                        new Title()
                        {
                            LanguageCode = "en",
                            MediaType = "text/plain",
                            Value = "Purge"
                        },
                    },
                    Action = "write",
                    IsDeleteDialogAction = true,
                    Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/purge",
                    HttpMethod = "DELETE",
                    Priority = "Tertiary"
                }
            };
            return guiActions;
        }

        private static List<Attachment> GetAttachmentsForCorrespondence(string baseUrl, CorrespondenceEntity correspondence)
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
                        ConsumerType = "Gui",
                        MediaType = "application/octet-stream",
                        Url = GetDownloadAttachmentEndpoint(baseUrl, correspondence.Id, attachment.AttachmentId)
                    }
                }
            }).ToList() ?? new List<Attachment>();
        }
    }
}
