using System.Text.Json.Serialization;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.API.Models
{
    public class MigrateCorrespondenceForwardingEventExt
    {
        /// <summary>
        /// The date and time for when the correspondence was forwarded.
        /// </summary>
        [JsonPropertyName("forwardedOnDate")]
        public required DateTimeOffset ForwardedOnDate { get; set; }

        /// <summary>
        /// PartyId of user that performed the forwarding action.
        /// </summary>
        [JsonPropertyName("forwardedByUserPartyUuid")]
        public Guid ForwardedByUserPartyUuid { get; set; }

        /// <summary>
        /// Optional PartyId of user that the correspondence was forwarded to.
        /// </summary>
        [JsonPropertyName("forwardedToUserPartyUuid")]
        public Guid? ForwardedToUserPartyUuid { get; set; }

        /// <summary>
        /// Optional Text used when forwarding the correspondence.
        /// </summary>
        [JsonPropertyName("forwardingText")]
        public string? ForwardingText { get; set; }

        /// <summary>
        /// Optional Email address that was used to notify the user that the correspondence was forwarded to.
        /// </summary>
        [JsonPropertyName("forwardedToEmailAddress")]
        public string? ForwardedToEmailAddress { get; set; }

        /// <summary>
        /// Optional Org number for the Mailbox supplier that the correspondence was forwarded to.
        /// </summary>
        [JsonPropertyName("mailboxSupplier")]
        [OrganizationNumber(ErrorMessage = $"Organization numbers should be on the format '{UrnConstants.OrganizationNumberAttribute}:organizationnumber' or the format countrycode:organizationnumber, for instance 0192:910753614")]        
        public string? MailboxSupplier { get; set; }
    }
}
