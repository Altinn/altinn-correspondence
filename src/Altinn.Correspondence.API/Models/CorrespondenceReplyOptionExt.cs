using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a ReplyOption with information provided by the sender.
    /// A reply option is a way for recipients to respond to a correspondence in addition to the normal Read and Confirm operations
    /// </summary>
    public class CorrespondenceReplyOptionExt
    {
        /// <summary>
        /// Gets or sets the URL to be used as a reply/response to a correspondence. 
        /// </summary>
        [IsLink]
        [JsonPropertyName("linkURL")]
        public required string LinkURL { get; set; }

        /// <summary>
        /// Gets or sets the url text.
        /// </summary>
        [JsonPropertyName("linkText")]
        public string? LinkText { get; set; }

        [AttributeUsage(AttributeTargets.Property)]
        internal class IsLinkAttribute : ValidationAttribute
        {
            private const int maxLength = 255;
            private const string httpsPrefix = "https://";
            private const string httpPrefix = "http://";
            public IsLinkAttribute()
            {

            }

            protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            {
                if (value is not string strValue)
                {
                    return new ValidationResult("LinkURL is not of type string");
                }
                if (strValue.Length > maxLength)
                {
                    return new ValidationResult($"LinkURL  must not exceed {maxLength} characters");
                }
                if (!Uri.IsWellFormedUriString((string)value, UriKind.Absolute))
                {
                    return new ValidationResult("LinkURL is not a valid URL");
                }
                if (strValue.StartsWith(httpPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult("LinkURL must use HTTPS");
                }
                if (!strValue.StartsWith(httpsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult($"LinkURL must start with{httpsPrefix}");
                }
                return ValidationResult.Success;
            }
        }
    }
}