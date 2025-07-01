using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    internal static class SystemLabel
    {
        internal static string Archived = "Archive";
        internal static string Default = "Default";
        internal static string Bin = "Bin";
    }

    internal static class CreateDialogRequestMapper
    {
        internal static CreateDialogRequest CreateCorrespondenceDialog(CorrespondenceEntity correspondence, string baseUrl, bool includeActivities = false)
        {
            var dialogId = Guid.CreateVersion7().ToString(); // Dialogporten requires time-stamped GUIDs
            bool isArchived = correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.Archived);
            
            return new CreateDialogRequest
            {
                Id = dialogId,
                ServiceResource = UrnConstants.Resource + ":" + correspondence.ResourceId,
                Party = correspondence.GetRecipientUrn(),
                CreatedAt = correspondence.Created,
                UpdatedAt = correspondence.Statuses?.Select(s => s.StatusChanged).DefaultIfEmpty().Concat([correspondence.Created]).Max(),
                VisibleFrom = correspondence.RequestedPublishTime < DateTime.UtcNow.AddMinutes(1) ? null : correspondence.RequestedPublishTime,
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
                Activities = includeActivities ? GetActivitiesForCorrespondence(correspondence) : new List<Activity>(),
                Transmissions = new List<Transmission>(),
                SystemLabel = isArchived ? SystemLabel.Archived : SystemLabel.Default
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
            SenderName = String.IsNullOrWhiteSpace(correspondence.MessageSender) ? null :
             new ContentValue()
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
            if (string.IsNullOrWhiteSpace(searchTag) || searchTag.Trim().Length < 3)
            {
                return list;
            }
            list.Add(
                new SearchTag()
                {
                    Value = searchTag.Trim()
                }
            );
            return list;
        }

        private static List<Activity> GetActivitiesForCorrespondence(CorrespondenceEntity correspondence)
        {
            List<Activity> activities = new();
            var orderedStatuses = correspondence.Statuses.OrderBy(s => s.StatusChanged);

            orderedStatuses.Where(s => s.Status == CorrespondenceStatus.Read).ToList().ForEach(s => activities.Add(GetActivityFromStatus(correspondence, s)));

            var confirmedStatus = orderedStatuses.FirstOrDefault(s => s.Status == CorrespondenceStatus.Confirmed);
            if(confirmedStatus != null)
            {
                activities.Add(GetActivityFromStatus(correspondence, confirmedStatus));
            }

            activities.AddRange(GetActivitiesFromNotifications(correspondence));

            return activities.OrderBy(a => a.CreatedAt).ToList();
        }

        private static Activity GetActivityFromStatus(CorrespondenceEntity correspondence, CorrespondenceStatusEntity status)
        {
            bool isConfirmation = status.Status == CorrespondenceStatus.Confirmed;

            Activity activity = new Activity();
            activity.Id = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString();
            activity.PerformedBy = new PerformedBy()
            {
                ActorId = correspondence.GetRecipientUrn(),
                ActorType = "PartyRepresentative"
            };
            activity.CreatedAt = status.StatusChanged;
            activity.Type = isConfirmation ? "CorrespondenceConfirmed" : "CorrespondenceOpened";
            activity.Description = [];

            return activity;
        }

        private static List<Activity> GetActivitiesFromNotifications(CorrespondenceEntity correspondence)
        {
            List<Activity> notificationActivities = new List<Activity>();
            foreach (var notification in correspondence.Notifications.Where(n => n.Altinn2NotificationId != null))
            {
                notificationActivities.Add(GetActivityFromAltinn2Notification(correspondence, notification));
            }

            return notificationActivities;
        }

        private static Activity GetActivityFromAltinn2Notification(CorrespondenceEntity correspondence, CorrespondenceNotificationEntity notification)
        {
            Activity activity = new Activity();
            activity.Id = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString();
            activity.PerformedBy = new PerformedBy()
            {
                ActorType = "ServiceOwner"
            };
            activity.CreatedAt = notification.NotificationSent ?? notification.RequestedSendTime;
            activity.Type = "Information";


            string[] tokens = [];
            if (notification.NotificationAddress != null)
            {
                tokens = [notification.NotificationAddress, notification.NotificationChannel == NotificationChannel.Email ? "Email" : "SMS"];
            }

            activity.Description =
            [
                new ()
                {
                    LanguageCode = "nb",
                    Value = DialogportenText.GetDialogportenText(DialogportenTextType.NotificationSent, Enums.DialogportenLanguageCode.NB, tokens)
                },
                new ()
                {
                    LanguageCode = "nn",
                    Value = DialogportenText.GetDialogportenText(DialogportenTextType.NotificationSent, Enums.DialogportenLanguageCode.NN, tokens)
                },
                new ()
                {
                    LanguageCode = "en",
                    Value = DialogportenText.GetDialogportenText(DialogportenTextType.NotificationSent, Enums.DialogportenLanguageCode.EN, tokens)
                },
            ];

            return activity;
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
                    Action = "read",
                    Endpoints = new List<Endpoint>()
                    {
                        new Endpoint()
                        {
                            HttpMethod = "GET",
                            Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}"
                        }
                    },

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
                }
            };
            if (correspondence.IsConfirmationNeeded) apiActions.Add(new ApiAction()
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
            });

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
            var guiActions = new List<GuiAction>();

            // Add ReplyOptions as GUI actions first
            if (correspondence.ReplyOptions != null && correspondence.ReplyOptions.Any())
            {
                // Add each ReplyOption from the request
                foreach (var replyOption in correspondence.ReplyOptions)
                {
                    guiActions.Add(new GuiAction()
                    {
                        Title = new List<Title>()
                        {
                            new Title()
                            {
                                LanguageCode = "nb",
                                MediaType = "text/plain",
                                Value = replyOption.LinkText ?? "Gå til tjeneste"
                            },
                            new Title()
                            {
                                LanguageCode = "nn",
                                MediaType = "text/plain",
                                Value = replyOption.LinkText ?? "Gå til teneste"
                            },
                            new Title()
                            {
                                LanguageCode = "en",
                                MediaType = "text/plain",
                                Value = replyOption.LinkText ?? "Go to service"
                            }
                        },
                        Action = "read",
                        Url = replyOption.LinkURL,
                        HttpMethod = "GET",
                        Priority = "Tertiary"
                    });
                }
            }
            if (correspondence.IsConfirmationNeeded)
            {
                guiActions.Add(new GuiAction()
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
                    Action = "read",
                    Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/confirm",
                    HttpMethod = "POST",
                    Priority = "Primary"
                });
            }

            guiActions.Add(new GuiAction()
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
                Action = "read",
                IsDeleteDialogAction = true,
                Url = $"{baseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/purge",
                HttpMethod = "DELETE",
                Priority = "Tertiary"
            });

            return guiActions;
        }

        private static List<Attachment> GetAttachmentsForCorrespondence(string baseUrl, CorrespondenceEntity correspondence)
        {
            return correspondence.Content?.Attachments.Select((attachment, index) => new Attachment
            {
                Id = Guid.CreateVersion7().ToString(),
                DisplayName = new List<DisplayName>
                {
                    new DisplayName
                    {
                        LanguageCode = correspondence.Content.Language,
                        Value = attachment.Attachment.DisplayName ?? attachment.Attachment.FileName
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