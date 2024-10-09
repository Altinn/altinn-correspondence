using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten
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
                VisibleFrom = correspondence.VisibleFrom < DateTime.UtcNow.AddMinutes(1) ? DateTime.UtcNow.AddMinutes(1) : correspondence.VisibleFrom,
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
            var searchTags = new List<SearchTag>
            {
                new SearchTag()
                {
                    Value = correspondence.SendersReference
                },
                new SearchTag()
                {
                    Value = correspondence.Sender
                },
                new SearchTag()
                {
                    Value = correspondence.Recipient
                },
                new SearchTag()
                {
                    Value = correspondence.ResourceId
                }
            };
            if (correspondence.MessageSender is not null)
            {
                searchTags.Add(new SearchTag()
                {
                    Value = correspondence.MessageSender.ToString()
                });
            }
            foreach (var property in correspondence.PropertyList)
            {
                searchTags.Add(new SearchTag()
                {
                    Value = property.Key
                });
                searchTags.Add(new SearchTag()
                {
                    Value = property.Value
                });
            }
            foreach (var reference in correspondence.ExternalReferences)
            {
                searchTags.Add(new SearchTag()
                {
                    Value = reference.ReferenceType.ToString()
                });
                searchTags.Add(new SearchTag()
                {
                    Value = reference.ReferenceValue
                });
            }
            searchTags = searchTags.DistinctBy(tag => tag.Value).ToList(); // Remove duplicates
            return searchTags;
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
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/confirm",
                            Deprecated = true
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
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/archive",
                            Deprecated = true
                        }
                    }
                }
            };
            foreach (var attachment in correspondence.Content?.Attachments)
            {
                apiActions.Add(new ApiAction()
                {
                    Action = "write",
                    Endpoints = new List<Endpoint>()
                    {
                        new Endpoint()
                        {
                            HttpMethod = "GET",
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/attachment/{attachment.Id}/download",
                            Deprecated = true
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
                }
            };
            foreach (var attachment in correspondence.Content?.Attachments)
            {
                guiActions.Add(new GuiAction()
                {
                    Title = new List<Title>()
                        {
                            new Title()
                            {
                                LanguageCode = "nb",
                                MediaType = "text/plain",
                                Value = "Last ned vedlegg"
                            },
                            new Title()
                            {
                                LanguageCode = "nn",
                                MediaType = "text/plain",
                                Value = "Last ned vedlegg"
                            },
                            new Title()
                            {
                                LanguageCode = "en",
                                MediaType = "text/plain",
                                Value = "Download Attachment"
                            },
                        },
                    Action = "read",
                    Url = GetDownloadAttachmentEndpoint(baseUrl, correspondence.Id, attachment.Id),
                    HttpMethod = "GET",
                    Priority = "Tertiary"
                });
            }
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
                        ConsumerType = "Api",
                        MediaType = "application/octet-stream",
                        Url = GetDownloadAttachmentEndpoint(baseUrl, correspondence.Id, attachment.Id)
                    }
                }
            }).ToList() ?? new List<Attachment>();
        }
    }
}
