﻿using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a notification connected to a specific correspondence
    /// </summary>
    public class NotificationExt
    {
        /// <summary>
        /// Gets or sets the id of the notification order
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the senders reference of the notification
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }

        /// <summary>
        /// Gets or sets the requested send time of the notification
        /// </summary>
        [JsonPropertyName("requestedSendTime")]
        public DateTime RequestedSendTime { get; set; }

        /// <summary>
        /// Gets or sets the short name of the creator of the notification order
        /// </summary>
        [JsonPropertyName("creator")]
        public string Creator { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date and time of when the notification order was created
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime Created { get; set; }

        /// <summary>
        /// whether the notification is a reminder notification
        /// </summary>
        [JsonPropertyName("isReminder")]
        public bool IsReminder { get; set; }

        /// <summary>
        /// Gets or sets the preferred notification channel of the notification order
        /// </summary>
        [JsonPropertyName("notificationChannel")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        /// Gets or sets whether notifications generated by this order should ignore KRR reservations
        /// </summary>
        [JsonPropertyName("ignoreReservation")]
        public bool? IgnoreReservation { get; set; }

        /// <summary>
        /// Gets or sets the id of the resource that the notification is related to
        /// </summary>
        [JsonPropertyName("resourceId")]
        public string? ResourceId { get; set; }

        /// <summary>
        /// Gets or sets the processing status of the notication order
        /// </summary>
        [JsonPropertyName("processingStatus")]
        public NotificationProcessStatusExt ProcessingStatus { get; set; }

        /// <summary>
        /// Gets or sets the summary of the notifiications statuses
        /// </summary>
        [JsonPropertyName("notificationStatusDetails")]
        public NotificationStatusDetailsExt? NotificationStatusDetails { get; set; }
    }
}
