namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents details about the status of a Correspondence. 
    /// </summary>
    public class CorrespondenceStatusDetailsExternalBEV2
    {
        /// <summary>
        /// Gets or sets the unique id of the correspondence
        /// </summary>
        public int CorrespondenceID { get; set; }

        /// <summary>
        /// Gets or sets the created date for the correspondence.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the reportee of the correspondence.
        /// </summary>
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets the party id for the reportee of the correspondence. 
        /// </summary>
        public int PartyId { get; set; }

        /// <summary>
        /// Gets or sets the senders reference on the correspondence.
        /// </summary>
        public string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets a list of status changes the correspondence has gone through.
        /// </summary>
        public List<CorrespondenceStatusChangeExternalBEV2> StatusChanges { get; set; }

        /// <summary>
        /// Gets or sets a list of notifications that has been sent to recipients regarding the correspondence.
        /// </summary>
        public List<NotificationDetailsExternalBE> Notifications { get; set; }
    }
}
