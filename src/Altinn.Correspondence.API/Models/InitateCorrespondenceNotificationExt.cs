﻿using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class InitateCorrespondenceNotificationExt
    {
        /// <summary>
        /// Which of the notifcation templates to use for this notification
        /// </summary>
        [JsonPropertyName("notificationTemplate")]
        public string NotificationTemplate { get; set; }

        /// <summary>
        /// Senders Reference for this notification
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }

        /// <summary>
        /// The channel for this notification
        /// </summary>
        [JsonPropertyName("notificationChannel")]
        public NotificationChannelExt NotificationChannel { get; set; }

        /// <summary>
        /// The date and time for when the notification should be sent.
        /// </summary>
        [JsonPropertyName("requestedSendTime")]
        public DateTime RequestedSendTime { get; set; }
    }
}
