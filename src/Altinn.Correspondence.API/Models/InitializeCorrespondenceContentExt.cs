using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents the content of a reportee element of the type correspondence.
    /// </summary>
    public class InitializeCorrespondenceContentExt
    {
        /// <summary>
        /// Gets or sets the language of the correspondence, specified according to ISO 639-1 
        /// </summary>
        [JsonPropertyName("language")]
        [ISO6391]
        public required string Language { get; set; }

        /// <summary>
        /// Gets or sets the correspondence message title. Subject.
        /// </summary>
        /// <remarks>
        /// TODO: Length restriction?
        /// </remarks>
        [JsonPropertyName("messageTitle")]
        public required string MessageTitle { get; set; }

        /// <summary>
        /// Gets or sets a summary text of the correspondence.
        /// </summary>
        /// <remarks>
        /// TODO: Length restriction?
        /// </remarks>
        [JsonPropertyName("messageSummary")]
        public required string MessageSummary { get; set; }

        /// <summary>
        /// Gets or sets the main body of the correspondence.
        /// </summary>
        public required string MessageBody { get; set; }

        /// <summary>
        /// Gets or sets a list of attachments.
        /// </summary>
        /// <remarks>
        /// TODO: Number restriction?
        /// </remarks>
        [JsonPropertyName("attachments")]
        public List<InitializeCorrespondenceAttachmentExt> Attachments { get; set; } = new List<InitializeCorrespondenceAttachmentExt>();
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class ISO6391Attribute : ValidationAttribute
    {
        public ISO6391Attribute()
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return new ValidationResult("The ISO6391 field cannot be null or empty!");
            }
            if (stringValue.Length != 2)
            {
                return new ValidationResult("The ISO6391 field must be exactly 2 characters long!");
            }
            return ValidationResult.Success;
        }
    }
}