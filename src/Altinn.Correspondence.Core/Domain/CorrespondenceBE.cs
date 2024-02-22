using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// The CorrespondenceBE provides Correspondence details.
    /// Each property listed has a one-on-one mapping with a column either from Correspondence or CorrespondenceServiceDetails
    /// or ReporteeElement or CorrespondenceConfirmation table in the Service Engine DB
    /// </summary>    
    public class CorrespondenceBE
    {
        /// <summary>
        /// Gets or sets Correspondence Identifier
        /// </summary>
        public int CorrespondenceID { get; set; }

        /// <summary>
        /// Gets or sets different kinds of status of a correspondence.
        /// </summary>
        public CorrespondenceStatusType CorrespondenceStatus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to be handled as unread.
        /// </summary>
        public bool MarkedUnRead { get; set; }

        /// <summary>
        /// Gets or sets Correspondence Reference in Archive DB
        /// </summary>
        public string ArchiveReference { get; set; }

        /// <summary>
        /// Gets or sets the correspondence languageID
        /// </summary>
        public LanguageType LanguageID { get; set; }

        /// <summary>
        /// Gets or sets Message Summary the Correspondence
        /// </summary>
        public string CorrespondenceSummary { get; set; }

        /// <summary>
        /// Gets or sets Text in the Correspondence
        /// </summary>
        public string CorrespondenceTxt { get; set; }

        /// <summary>
        /// Gets or sets Correspondence Header
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// Gets or sets the sender who sent this correspondence
        /// </summary>
        public string SentBy { get; set; }

        /// <summary>
        /// Gets or sets Correspondence Subject
        /// </summary>
        public string CorrespondenceSubject { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Correspondence needs Confirmation or Not
        /// </summary>
        public bool IsConfirmationNeeded { get; set; }

        /// <summary>
        /// Gets or sets to whom this correspondence was sent - Reportee
        /// </summary>
        public int SentTo { get; set; }

        /// <summary>
        /// Gets or sets Date on which the correspondence was sent by the Service Owner
        /// </summary>
        public DateTime DateSent { get; set; }

        /// <summary>
        /// Gets or sets the Correspondence is due date
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Gets or sets who is the reportee.
        /// </summary>
        public int Reportee { get; set; }

        /// <summary>
        /// Gets or sets who is the authenticated user.
        /// </summary>
        public int? AuthenticatedUser { get; set; }

        /// <summary>
        /// Gets or sets Date on which Correspondence was confirmed,
        /// if at all the Correspondence needed confirmation
        /// </summary>
        public DateTime? ConfirmationDate { get; set; }

        /// <summary>
        /// Gets or sets the user who confirmed the correspondence
        /// </summary>
        public int? UserID { get; set; }

        /// <summary>
        /// Gets or sets the Service Owner Name
        /// </summary>
        public string Description { get; set; }

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

        /// <summary>
        /// Gets or sets Special information
        /// </summary>
        public string CustomMessageData { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the information to check Snooping Required or not
        /// </summary>
        public bool IsSnoopingRequired { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Correspondence can be forwarded via email or Not
        /// </summary>
        public bool AllowForwarding { get; set; }

        /// <summary>
        /// Gets or sets CaseID for current case.
        /// </summary>
        public int? CaseID { get; set; }

        /// <summary>
        /// Gets or sets Message Sender is the agency which sent the Correspondence, only defined if the Correspondence service is a generic service
        /// </summary>
        public string MessageSender { get; set; }

        /// <summary>
        /// Gets or sets SEReporteeElement ID
        /// </summary>
        public int SEReporteeElementID { get; set; }

        /// <summary>
        /// Gets or sets PresentationDateTime for archived elements in reportee archive this will contain the Highest DateTime of CreatedDateTime and VisibleDateTime from ServiceEngine
        /// </summary>
        public DateTime PresentationDateTime { get; set; }

        /// <summary>
        /// Gets or sets external service edition code
        /// </summary>
        public int ExternalServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets external service code
        /// </summary>
        public string ExternalServiceCode { get; set; }

        /// <summary>
        /// Gets or sets service owner
        /// </summary>
        public int ServiceOwnerID { get; set; }
    }

    /// <summary>
    /// The ReporteeElementBEList inherits List&lt;ReporteeElementBE&gt; and contains a number of ReporteeElementBE objects.
    /// In addition to the objects themselves, it contains the property LimitReached, which will tell if the result set
    /// size exceeded the maximum number of elements.
    /// </summary>
    public class CorrespondenceBEList : List<CorrespondenceBE>
    {
    }
}