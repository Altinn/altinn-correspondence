using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents the content of a Correspondence.
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
        [JsonPropertyName("messageTitle")]
        public required string MessageTitle { get; set; }

        /// <summary>
        /// Gets or sets a summary text of the correspondence.
        /// </summary>
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
        /// Maximum of 100 attachments allowed.
        /// </remarks>
        [JsonPropertyName("attachments")]
        [MaxListCount(100, "attachments")]
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
                return ValidationResult.Success;
            }
            if (stringValue.Length != 2)
            {
                return new ValidationResult("The ISO6391 field must be exactly 2 characters long!");
            }
            if (CultureInfo.InvariantCulture.TwoLetterISOLanguageName.Contains(stringValue))
            {
                return new ValidationResult("The language code must be ISO6391 compliant!");
            }
            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    internal class MaxListCountAttribute(int maxCount, string? propertyName = null) : ValidationAttribute
    {
        public int MaxCount { get; } = maxCount;
        public string PropertyName { get; } = propertyName ?? "items";

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is ICollection collection)
            {
                if (collection.Count > MaxCount)
                {
                    return new ValidationResult($"Maximum of {MaxCount} {PropertyName} allowed.");
                }
            }
            return ValidationResult.Success;
        }
    }
}