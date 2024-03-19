﻿using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// A more detailed object representing all the details for a correspondence, including status history and notificiations
    /// </summary>
    public class CorrespondenceDetailsExt : CorrespondenceOverviewExt
    {
        /// <summary>
        /// The Status history for the Corrrespondence
        /// </summary>
        [JsonPropertyName("statusHistory")]
        public List<CorrespondenceStatusEventExt> StatusHistory { get; set; }


        /// <summary>
        /// The Status history for the Notifications
        /// </summary>
        [JsonPropertyName("notificationStatusHistory")]
        public List<NotificationStatusEventExt> NotificationStatusHistory { get; set; }
    }
}
