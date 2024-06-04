using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a request object for the operation, InitializeCorrespondence, that can create a correspondence in Altinn.    
    /// </summary>
    public class InitializeCorrespondenceExt
    {
        /// <summary>
        /// Gets or sets the Resource Id for the correspondence service.
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        /// <summary>
        /// The recipient of the correspondence, either an organisation or an person
        /// </summary>
        /// <remarks>
        /// National identity number or Organization number.
        /// </remarks
        [JsonPropertyName("recipient")]
        [Required]
        public required string Recipient { get; set; }

        /// <summary>
        /// The Sending organisation of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Organization number in countrycode:organizationnumber format.
        /// </remarks>
        [JsonPropertyName("sender")]
        [RegularExpression(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public required string Sender { get; set; }

        /// <summary>
        /// Used by senders and receivers to identify specific a Correspondence using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        /// <summary>
        /// The correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        [JsonPropertyName("content")]
        public InitializeCorrespondenceContentExt? Content { get; set; }

        /// <summary>
        /// When the correspondence should become visible to the recipient.
        /// </summary>
        [JsonPropertyName("visibleFrom")]
        public required DateTimeOffset VisibleFrom { get; set; }

        /// <summary>
        /// Gets or sets the date for when Altinn can remove the correspondence from its database.
        /// </summary>
        [JsonPropertyName("allowSystemDeleteAfter")]
        public DateTimeOffset? AllowSystemDeleteAfter { get; set; }

        /// <summary>
        /// Gets or sets a date and time for when the recipient must reply.
        /// </summary>
        [JsonPropertyName("dueDateTime")]
        public DateTimeOffset DueDateTime { get; set; }

        /// <summary>
        /// Gets or sets an list of references Senders can use this field to tell the recipient that the correspondence is related to the referenced item(s)
        /// Examples include Altinn App instances, Altinn Broker File Transfers
        /// </summary>
        /// <remarks>
        /// TODO: Do we need this on Attachments for DialogPorten etc?
        /// </remarks>
        [JsonPropertyName("externalReferences")]
        public List<ExternalReferenceExt>? ExternalReferences { get; set; }

        /// <summary>
        /// User-defined properties related to the Correspondence
        /// </summary>
        [JsonPropertyName("propertyList")]
        [PropertyList]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Options for how the recipient can reply to the correspondence
        /// </summary>
        [JsonPropertyName("replyOptions")]
        public List<CorrespondenceReplyOptionExt> ReplyOptions { get; set; } = new List<CorrespondenceReplyOptionExt>();

        /// <summary>
        /// Notifications directly related to this Correspondence.
        /// </summary>
        [JsonPropertyName("notifications")]
        [MaxLength(6, ErrorMessage = "Notifications can contain at most 6 notifcations")]
        public List<InitializeCorrespondenceNotificationExt> Notifications { get; set; } = new List<InitializeCorrespondenceNotificationExt>();

        /// <summary>
        /// Specifies whether the correspondence can override reservation against digital comminication in KRR
        /// </summary>
        [JsonPropertyName("isReservable")]
        public bool? IsReservable { get; set; }
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
}