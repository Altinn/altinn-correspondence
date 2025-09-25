using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using UUIDNext;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Helpers;

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
        internal static CreateDialogRequest CreateCorrespondenceDialog(CorrespondenceEntity correspondence, string baseUrl, bool includeActivities = false, ILogger? logger = null, string? openedActivityIdempotencyKey = null, string? confirmedActivityIdempotencyKey = null, bool isSoftDeleted = false)
        {
            var dialogId = Guid.CreateVersion7().ToString(); // Dialogporten requires time-stamped GUIDs
            DateTimeOffset? dueAt = correspondence.DueDateTime != default ? correspondence.DueDateTime : null;

            // The problem of DueAt being in the past should only occur for migrated data, as such we are checking includeActivities flag first, since this is only set when making migrated correspondences available.
            if (includeActivities && dueAt.HasValue && dueAt < DateTimeOffset.Now)
            {
                dueAt = null;
            }

            return new CreateDialogRequest
            {
                Id = dialogId,
                ServiceResource = UrnConstants.Resource + ":" + correspondence.ResourceId,
                Party = correspondence.GetRecipientUrn(),
                CreatedAt = correspondence.Created,
                UpdatedAt = (correspondence.Statuses ?? []).Select(s => s.StatusChanged).Concat([correspondence.Created]).Max(),
                VisibleFrom = correspondence.RequestedPublishTime < DateTime.UtcNow.AddMinutes(1) ? null : correspondence.RequestedPublishTime,
                Process = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenProcessId)?.ReferenceValue,
                DueAt = dueAt,
                Status = GetDialogStatusForCorrespondence(correspondence),
                ExternalReference = correspondence.SendersReference,
                Content = CreateCorrespondenceContent(correspondence, baseUrl),
                SearchTags = GetSearchTagsForCorrespondence(correspondence, logger),
                ApiActions = GetApiActionsForCorrespondence(baseUrl, correspondence),
                GuiActions = GetGuiActionsForCorrespondence(baseUrl, correspondence),
                Attachments = GetAttachmentsForCorrespondence(baseUrl, correspondence),
                Activities = includeActivities ? GetActivitiesForCorrespondence(correspondence, openedActivityIdempotencyKey, confirmedActivityIdempotencyKey) : new List<Activity>(),
                Transmissions = new List<Transmission>(),
                SystemLabel = GetSystemLabelForCorrespondence(correspondence, isSoftDeleted)
            };
        }

        private static string GetSystemLabelForCorrespondence(CorrespondenceEntity correspondence, bool isSoftDeleted)
        {
            if (correspondence.Altinn2CorrespondenceId.HasValue) // Only relevant for migrated correspondences
            {
                if (isSoftDeleted)
                {
                    return SystemLabel.Bin;
                }
                if (correspondence.Statuses != null && correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.Archived))
                {
                    return SystemLabel.Archived;
                }
            }

            return SystemLabel.Default;
        }

        private static string GetDialogStatusForCorrespondence(CorrespondenceEntity correspondence)
        {
            if (correspondence.IsConfirmationNeeded)
            {
                return "RequiresAttention";
            }
            return "NotApplicable";
        }

        private static Content CreateCorrespondenceContent(CorrespondenceEntity correspondence, string baseUrl) => new()
        {
            Title = new ContentValue()
            {
                MediaType = "text/plain",
                Value = new List<DialogValue> {
                    new DialogValue()
                    {
                        Value = TruncateTitleForDialogporten(correspondence.Content!.MessageTitle ?? ""),
                        LanguageCode = correspondence.Content.Language
                    }
                }
            },
            Summary = string.IsNullOrWhiteSpace(correspondence.Content.MessageSummary) ? null : new ContentValue()
            {
                MediaType = "text/plain",
                Value = new List<DialogValue> {
                    new DialogValue()
                    {
                        Value = StripSummaryForHtmlAndMarkdown(correspondence.Content.MessageSummary ?? ""),
                        LanguageCode = correspondence.Content.Language
                    }
                }
            },
            SenderName = string.IsNullOrWhiteSpace(correspondence.MessageSender) ? null :
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

        private static List<SearchTag> GetSearchTagsForCorrespondence(CorrespondenceEntity correspondence, ILogger? logger)
        {
            var list = new List<SearchTag>();
            list = AddSearchTagIfValid(list, correspondence.SendersReference, correspondence, logger);
            list = AddSearchTagIfValid(list, correspondence.Sender, correspondence, logger);
            list = AddSearchTagIfValid(list, correspondence.ResourceId, correspondence, logger);
            list = AddSearchTagIfValid(list, correspondence.MessageSender, correspondence, logger);
            foreach (var reference in correspondence.ExternalReferences)
            {
                list = AddSearchTagIfValid(list, reference.ReferenceType.ToString(), correspondence, logger);
                list = AddSearchTagIfValid(list, reference.ReferenceValue.ToString(), correspondence, logger);
            }
            list = list.DistinctBy(tag => tag.Value).ToList(); // Remove duplicates
            return list;
        }

        private static List<SearchTag> AddSearchTagIfValid(List<SearchTag> list, string? searchTag, CorrespondenceEntity correspondence, ILogger? logger)
        {
            if (string.IsNullOrWhiteSpace(searchTag) || searchTag.Trim().Length < 3)
            {
                return list;
            }
            var trimmed = searchTag.Trim();
            if (trimmed.Length > 63)
            {
                logger?.LogWarning("Truncating Dialogporten search tag for correspondence {CorrespondenceId} from {OriginalLength} to 63 characters", correspondence.Id, trimmed.Length);
                trimmed = trimmed.Substring(0, 63);
            }
            list.Add(new SearchTag() { Value = trimmed });
            return list;
        }

        private static List<Activity> GetActivitiesForCorrespondence(CorrespondenceEntity correspondence, string? openedActivityIdempotencyKey = null, string? confirmedActivityIdempotencyKey = null)
        {
            List<Activity> activities = new();
            var orderedStatuses = correspondence.Statuses.OrderBy(s => s.StatusChanged);

            var readStatus = orderedStatuses.FirstOrDefault(s => s.Status == CorrespondenceStatus.Read);
            if (readStatus != null && !string.IsNullOrWhiteSpace(openedActivityIdempotencyKey))
            {
                activities.Add(GetActivityFromStatus(correspondence, readStatus, openedActivityIdempotencyKey));
            }

            var confirmedStatus = orderedStatuses.FirstOrDefault(s => s.Status == CorrespondenceStatus.Confirmed);
            if (confirmedStatus != null && !string.IsNullOrWhiteSpace(confirmedActivityIdempotencyKey))
            {
                activities.Add(GetActivityFromStatus(correspondence, confirmedStatus, confirmedActivityIdempotencyKey));
            }

            activities.AddRange(GetActivitiesFromNotifications(correspondence));

            return activities.OrderBy(a => a.CreatedAt).ToList();
        }

        private static Activity GetActivityFromStatus(CorrespondenceEntity correspondence, CorrespondenceStatusEntity status, string? activityId = null)
        {
            bool isConfirmation = status.Status == CorrespondenceStatus.Confirmed;

            Activity activity = new Activity();
            activity.Id = string.IsNullOrWhiteSpace(activityId) ? Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString() : activityId;
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

        /// <summary>
        /// Truncates titles longer than 255 characters to 252 characters and adds "..." to fit within Dialogporten's 255 character limit.
        /// Titles 255 characters or shorter are sent as-is to Dialogporten.
        /// This serves as a safety net for existing correspondence with long titles that failed Dialog Porten creation,
        /// allowing them to retry successfully. New correspondence requests are validated to prevent titles > 255 chars.
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

        private static string StripSummaryForHtmlAndMarkdown(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Convert Markdown to HTML
            string withoutMarkdown = TextValidation.ConvertToHtml(input);
            // Remove HTML tags
            string withoutHtml = Regex.Replace(withoutMarkdown, @"<[^>]*>", string.Empty);

            // Clean up extra whitespace
            return Regex.Replace(withoutHtml, @"\s+", " ").Trim();

        }        
    }
}