namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// Represents a secure digital post element with key values and status history.
    /// </summary>
    public class SdpStatusDetailsBE
    {
        /// <summary>
        /// Gets or sets the unique id of a secure digital post element as it is stored in Altinn.
        /// </summary>
        public int SdpId { get; set; }

        /// <summary>
        /// Gets or sets unique id of the correspondence that was created at the same time as the digital letter. This is null
        /// if no correspondence was created.
        /// </summary>
        public int? CorrespondenceId { get; set; }

        /// <summary>
        /// Gets or sets the date for when the digital letter was created.
        /// </summary>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the date and time for when the digital letter element was updated in Altinn. This is updated with every status change.
        /// </summary>
        public DateTime LastChangedDateTime { get; set; }

        /// <summary>
        /// Gets or sets a identifying value for the reportee. This can be a social security number or organization number.
        /// </summary>
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets the party id of the reportee.
        /// </summary>
        public int ReporteeId { get; set; }

        /// <summary>
        /// Gets or sets the reference value that was provided by the agency that created the secure digital post element. 
        /// ExternalShipmentReference or SendersReference.
        /// </summary>
        public string Reference { get; set; }

        /// <summary>
        /// Gets or sets the status history for the secure digital post element.
        /// </summary>
        public List<SdpStatusChangeBE> StatusHistory { get; set; }
    }
}
