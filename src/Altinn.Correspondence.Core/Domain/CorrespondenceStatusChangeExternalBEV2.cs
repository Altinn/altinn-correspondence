using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents a status change on a correspondence element.
    /// </summary>
    public class CorrespondenceStatusChangeExternalBEV2
    {
        /// <summary>
        /// Gets or sets the date for when the status was changed to the given value.
        /// </summary>
        public DateTime StatusDate { get; set; }

        /// <summary>
        /// Gets or sets the status that were set.
        /// </summary>
        public CorrespondenceStatusTypeAgencyExternalV2 StatusType { get; set; }
    }
}
