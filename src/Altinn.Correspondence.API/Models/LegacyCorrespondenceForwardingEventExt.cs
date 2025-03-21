﻿using System.Text.Json.Serialization;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.API.Models
{
    public class LegacyCorrespondenceForwardingEventExt
    {
        /// <summary>
        /// Optional Text used when forwarding the correspondence.
        /// </summary>
        [JsonPropertyName("forwardingText")]
        public string? ForwardingText { get; set; }

        /// <summary>
        /// The name of the user that performed the forwarding action.
        /// </summary>
        [JsonPropertyName("forwardedBy")]
        public string ForwardedBy{ get; set; }

        /// <summary>
        /// The name of the user that the correspondence was forwarded to.
        /// </summary>
        [JsonPropertyName("forwardedTo")]
        public string? ForwardedTo { get; set; }       

        /// <summary>
        /// Optional Email address that was used to notify the user that the correspondence was forwarded to.
        /// </summary>
        [JsonPropertyName("forwardedToEmail")]
        public string? ForwardedToEmail { get; set; }

        /// <summary>
        /// Optional Name of the Mailbox supplier that the correspondence was forwarded to.
        /// </summary>
        [JsonPropertyName("mailboxSupplier")]        
        public string? MailboxSupplier { get; set; }
    }
}
