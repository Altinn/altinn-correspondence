﻿using System.Text.Json.Serialization;

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
        public List<CorrespondenceStatusEventExt> StatusHistory { get; set; } = new List<CorrespondenceStatusEventExt>();

        /// <summary>
        /// Notifications directly related to this Correspondence.
        /// </summary>
        [JsonPropertyName("notifications")]
        public new List<CorrespondenceNotificationExt>? Notifications { get; set; } = new List<CorrespondenceNotificationExt>();
    }
}
