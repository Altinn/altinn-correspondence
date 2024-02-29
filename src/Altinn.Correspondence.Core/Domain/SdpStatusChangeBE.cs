using Altinn.Correspondence.Core.Domain.Models.Enums;

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// Business entity containing information about a single status change to a secure digital post element.
    /// </summary>
    public class SdpStatusChangeBE
    {
        /// <summary>
        /// Gets or sets the status value.
        /// </summary>
        public SdpStatusType Status { get; set; }

        /// <summary>
        /// Gets or sets the date and time for when the status was set.
        /// </summary>
        public DateTime StatusDateTime { get; set; }
    }
}
