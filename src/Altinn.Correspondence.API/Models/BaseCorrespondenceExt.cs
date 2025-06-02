using Altinn.Correspondence.Common.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a request object for the operation, InitializeCorrespondence, that can create a correspondence in Altinn.    
    /// </summary>
    public class BaseCorrespondenceExt
    {
        /// <summary>
        /// The Resource Id associated with the correspondence service.
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        /// <summary>
        /// The Sending organization of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Organization number must be formatted as countrycode:organizationnumber.
        /// </remarks>
        [JsonPropertyName("sender")]
        [OrganizationNumber(ErrorMessage = $"Organization numbers should be on the format '{UrnConstants.OrganizationNumberAttribute}:organizationnumber' or the format countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public required string Sender { get; set; }

        /// <summary>
        /// A reference used by senders and receivers to identify a specific Correspondence using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        /// <summary>
        /// An alternative name for the sender of the correspondence. The name will be displayed instead of the organization name.
        ///  </summary>
        [JsonPropertyName("messageSender")]
        [StringLength(256, MinimumLength = 0)]
        public string? MessageSender { get; set; }

        /// <summary>
        /// The correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        [JsonPropertyName("content")]
        public InitializeCorrespondenceContentExt? Content { get; set; }

        /// <summary>
        /// When the correspondence should become visible to the recipient.
        /// </summary>
        [JsonPropertyName("requestedPublishTime")]
        public DateTimeOffset? RequestedPublishTime { get; set; }

        /// <summary>
        /// When Altinn can remove the correspondence from its database.
        /// </summary>
        [JsonPropertyName("allowSystemDeleteAfter")]
        public DateTimeOffset? AllowSystemDeleteAfter { get; set; }

        /// <summary>
        /// When the recipient must reply to the correspondence
        /// </summary>
        [JsonPropertyName("dueDateTime")]
        public DateTimeOffset? DueDateTime { get; set; }

        /// <summary>
        /// A list of references Senders can use to tell the recipient that the correspondence is related to the referenced item(s)
        /// Examples include Altinn App instances, Altinn Broker File Transfers
        /// </summary>
        [JsonPropertyName("externalReferences")]
        [ExternalReferences]
        public List<ExternalReferenceExt>? ExternalReferences { get; set; }

        /// <summary>
        /// User-defined properties related to the Correspondence
        /// </summary>
        [JsonPropertyName("propertyList")]
        [PropertyList]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Options for how the recipient can reply to the Correspondence
        /// </summary>
        [JsonPropertyName("replyOptions")]
        public List<CorrespondenceReplyOptionExt>? ReplyOptions { get; set; } = new List<CorrespondenceReplyOptionExt>();

        /// <summary>
        /// Notifications related to the Correspondence.
        /// </summary>
        [JsonPropertyName("notification")]
        public InitializeCorrespondenceNotificationExt? Notification { get; set; }

        /// <summary>
        /// Specifies whether the correspondence can override reservation against digital communication in KRR
        /// </summary>
        [JsonPropertyName("ignoreReservation")]
        public bool? IgnoreReservation { get; set; }

        /// <summary>
        /// Is null until the correspondence is published.
        /// </summary>
        [JsonPropertyName("published")]
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Specifies whether reading the correspondence needs to be confirmed by the recipient
        /// </summary>
        [JsonPropertyName("isConfirmationNeeded")]
        public bool IsConfirmationNeeded { get; set; }
        
        /// <summary>
        /// Specifies whether the correspondence is confidential
        /// </summary>
        [JsonPropertyName("isConfidential")]
        public bool IsConfidential { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class PropertyListAttribute : ValidationAttribute
    {
        public PropertyListAttribute()
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            if (!(value is Dictionary<string, string>))
            {
                return new ValidationResult("propertyList Object is not of proper type");
            }

            var dictionary = (Dictionary<string, string>)value;

            if (dictionary.Count > 10)
                return new ValidationResult("propertyList can contain at most 10 properties");

            foreach (var keyValuePair in dictionary)
            {
                if (keyValuePair.Key.Length > 50)
                    return new ValidationResult(String.Format("propertyList Key can not be longer than 50. Length:{0}, KeyValue:{1}", keyValuePair.Key.Length.ToString(), keyValuePair.Key));

                if (keyValuePair.Value.Length > 300)
                    return new ValidationResult(String.Format("propertyList Value can not be longer than 300. Length:{0}, Value:{1}", keyValuePair.Value.Length.ToString(), keyValuePair.Value));
            }

            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class ExternalReferencesAttribute : ValidationAttribute
    {
        public ExternalReferencesAttribute()
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success;

            if (!(value is List<ExternalReferenceExt>))
                return new ValidationResult("externalReferences is not of proper type");

            var externalReferences = (List<ExternalReferenceExt>)value;
            if (externalReferences.Count > 10)
                return new ValidationResult("externalReferences can contain at most 10 references");
            if (externalReferences.Any(externalReference => externalReference.ReferenceType == Enums.ReferenceTypeExt.DialogportenDialogId))
                return new ValidationResult("Cannot initialize a correspondence with pre-existing dialog element defined");

            return ValidationResult.Success;
        }
    }
}
