using Altinn.Correspondence.Core.Domain.Models.Enums;

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// The CorrespondenceConfirmationBE provides Correspondence details which is needed when we send confirmations back to a service owner.
    /// Each property listed has a one-on-one mapping with a column either from Correspondence or CorrespondenceServiceDetails
    /// or ReporteeElement or CorrespondenceConfirmation table in the Service Engine DB
    /// </summary>
    public class CorrespondenceConfirmationBE
    {
        /// <summary>
        /// Gets or sets The externally known service code
        /// </summary>
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets The externally known service edition
        /// </summary>
        public int ServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets different statuses of a correspondence.
        /// </summary>
        public CorrespondenceStatusType CorrespondenceStatus { get; set; }

        /// <summary>
        /// Gets or sets Correspondence Reference in Archive DB
        /// </summary>
        public string ArchiveReference { get; set; }

        /// <summary>
        /// Gets or sets the correspondence languageID
        /// </summary>
        public LanguageType LanguageID { get; set; }

        /// <summary>
        /// Gets or sets Correspondence Subject
        /// </summary>
        public string CorrespondenceSubject { get; set; }

        /// <summary>
        /// Gets or sets reportee to whom this correspondence was sent
        /// </summary>
        public string SentTo { get; set; }

        /// <summary>
        /// Gets or sets Date on which the correspondence was sent by the Service Owner
        /// </summary>
        public DateTime DateSent { get; set; }

        /// <summary>
        /// Gets or sets who is the reportee.
        /// </summary>
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets Date on which Correspondence was confirmed,
        /// if at all the Correspondence needed confirmation
        /// </summary>
        public DateTime? ConfirmationDate { get; set; }

        /// <summary>
        /// Gets or sets the user who confirmed the correspondence
        /// </summary>
        public string ConfirmingUser { get; set; }

        /// <summary>
        /// Gets or sets the name of the Correspondence
        /// </summary>
        public string CorrespondenceName { get; set; }

        /// <summary>
        /// Gets or sets the reference of the External system.
        /// </summary>
        public string ExternalSystemReference { get; set; }

        /// <summary>
        /// Gets or sets Message Title the Correspondence
        /// </summary>
        public string CorrespondenceTitle { get; set; }
    }
}