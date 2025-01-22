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
            public IsLinkAttribute()
            {
            }

            protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            {
                if (value == null || value.GetType() != typeof(string))
                {
                    return new ValidationResult("LinkURL is not of type string");
                }
                if (((string)value).Length > 255)
                {
                    return new ValidationResult("LinkURL is too long");
                }
                if (!Uri.IsWellFormedUriString((string)value, UriKind.Absolute))
                {
                    return new ValidationResult("LinkURL is not a valid URL");
                }
                if (((string)value).Contains(" "))
                {
                    return new ValidationResult("LinkURL contains whitespace");
                }
                if (((string)value).StartsWith("http://"))
                {
                    return new ValidationResult("LinkURL is not secure");
                }
                if (!((string)value).StartsWith("https://"))
                {
                    return new ValidationResult("LinkURL must start with https://");
                }
                return ValidationResult.Success;
            }
        }
    }
}