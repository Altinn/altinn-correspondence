using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a request object for the operation, InitializeCorrespondence, that can create a correspondence in Altinn.    
    /// </summary>
    public class UploadCorrespondenceExt
    {
        /// <summary>
        /// Correspondence data
        /// </summary>
        [JsonPropertyName("Correspondence")]
        public InitializeCorrespondenceExt Correspondence { get; set; }
        /// <summary>
        /// List of attachments to upload 
        /// </summary>
        [JsonPropertyName("Attachments")]
        public List<IFormFile> Attachments { get; set; }
    }
}