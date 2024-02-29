using Altinn.Correspondence.Core.Domain.Models.Enums;

namespace Altinn.Correspondence.Core.Domain.Models
{ 
    /// <summary>
    /// Represents the filtering choices an agency can use
    /// </summary>
    public class CorrespondenceStatusFilterBE
    {
        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the service code. This field is mandatory.
        /// </summary>
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the service edition code. This field is mandatory.
        /// </summary>
        public int ServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the senders reference value on the correspondence.
        /// </summary>
        public string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the recipient of the correspondence.
        /// </summary>
        /// <remarks>
        /// Value must be an organization number or social security number.
        /// </remarks>
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets the party id of the given Reportee. 
        /// </summary>
        /// <remarks>
        /// The field is used internally which is why it doesn't have the "DataMember" attribute.
        /// </remarks>
        public int ReporteeId { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the creation date of the correspondence.
        /// Includes correspondence newer than the set date.
        /// </summary>
        public DateTime? CreatedAfterDate { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the creation date of the correspondence.
        /// Includes correspondence older than the set date.
        /// </summary>
        public DateTime? CreatedBeforeDate { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the current status of the correspondence.
        /// </summary>
        public CorrespondenceStatusType CurrentStatus { get; set; }

        /// <summary>
        /// Gets or sets a value used to filter correspondence based on the status of notifications.
        /// </summary>
        public bool? NotificationSent { get; set; }

        /// <summary>
        /// Gets or sets a data object with information on how to include SDP information in the status search results.
        /// </summary>
        public SdpStatusSearchOptionsBE SdpSearchOptions { get; set; }
    }
}
