using System.Runtime.Serialization;

using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Represents details about the status of a Correspondence. 
    /// </summary>
    public class CorrespondenceStatusDetailsExternalBE
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
        public List<CorrespondenceStatusChangeExternalBE> StatusChanges { get; set; }

        /// <summary>
        /// Gets or sets a list of notifications that has been sent to recipients regarding the correspondence.
        /// </summary>
        public List<NotificationDetailsExternalBE> Notifications { get; set; }
    }

    /// <summary>
    /// Represents the response from the GetCorrespondenceStatusDetails operation in the CorrespondenceAgency service.
    /// </summary>
    public class CorrespondenceStatusDetailsResultExternalBE
    {
        /// <summary>
        /// Gets or sets the service code for the correspondences in the list.
        /// </summary>
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets the service edition code for the correspondences in the list.
        /// </summary>
        public int ServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets the list of correspondences and their statuses.
        /// </summary>
        public List<CorrespondenceStatusDetailsExternalBE> StatusList { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the result set is larger than the list can hold.
        /// </summary>
        public bool LimitReached { get; set; }

        /// <summary>
        /// Create a new instance of the CorrespondenceStatusDetailsResultExternalBE class with data from a CorrespondenceStatusDetailsResultExternalBEV2 object.
        /// </summary>
        /// <param name="newVersionFilterResult">The CorrespondenceStatusDetailsResultExternalBEV2 object to get initialization data from.</param>
        /// <returns>A new, populated CorrespondenceStatusDetailsResultExternalBE object.</returns>
        public static CorrespondenceStatusDetailsResultExternalBE Create(CorrespondenceStatusDetailsResultExternalBEV3 newVersionFilterResult)
        {
            List<CorrespondenceStatusDetailsExternalBE> correspondenceStatusList = new List<CorrespondenceStatusDetailsExternalBE>();

            //Remove the reserved status correspondences from the results
            newVersionFilterResult.CorrespondenceStatusInformation.CorrespondenceStatusDetailsList
                .RemoveAll(x => x.StatusChanges.FindAll(y => y.StatusType == CorrespondenceStatusTypeAgencyExternalV2.Reserved).Count > 0);

            //Map new versioned data to CorrespondenceStatusDetailsResultExternalBE
            foreach (CorrespondenceStatusDetailsExternalBEV2 correspondenceStatus in newVersionFilterResult.CorrespondenceStatusInformation.CorrespondenceStatusDetailsList)
            {
                CorrespondenceStatusDetailsExternalBE correspondenceStatusDetailsExternalBE = new CorrespondenceStatusDetailsExternalBE
                {
                    CorrespondenceID = correspondenceStatus.CorrespondenceID,
                    SendersReference = correspondenceStatus.SendersReference,
                    CreatedDate = correspondenceStatus.CreatedDate,
                    PartyId = correspondenceStatus.PartyId,
                    Reportee = correspondenceStatus.Reportee,
                    Notifications = correspondenceStatus.Notifications,
                    StatusChanges = new List<CorrespondenceStatusChangeExternalBE>()
                };

                //Map status change data
                foreach (CorrespondenceStatusChangeExternalBEV2 newVersionStatusChange in correspondenceStatus.StatusChanges)
                {
                    CorrespondenceStatusChangeExternalBE correspondenceStatusChangeExternalBE = new CorrespondenceStatusChangeExternalBE()
                    {
                        StatusType = (CorrespondenceStatusTypeAgencyExternal)((int)newVersionStatusChange.StatusType),
                        StatusDate = newVersionStatusChange.StatusDate
                    };

                    correspondenceStatusDetailsExternalBE.StatusChanges.Add(correspondenceStatusChangeExternalBE);
                }

                correspondenceStatusList.Add(correspondenceStatusDetailsExternalBE);
            }

            CorrespondenceStatusDetailsResultExternalBE filterResult = new CorrespondenceStatusDetailsResultExternalBE
            {
                StatusList = correspondenceStatusList,
                ServiceCode = newVersionFilterResult.ServiceCode,
                ServiceEditionCode = newVersionFilterResult.ServiceEditionCode,
                LimitReached = newVersionFilterResult.CorrespondenceStatusInformation.LimitReached
            };

            return filterResult;
        }
    }

    /// <summary>
    /// Represents a status change on a correspondence element.
    /// </summary>
    [DataContract(Name = "StatusChange", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2013/11")]
    public class CorrespondenceStatusChangeExternalBE
    {
        /// <summary>
        /// Gets or sets the date for when the status was changed to the given value.
        /// </summary>
        [DataMember]
        public DateTime StatusDate { get; set; }

        /// <summary>
        /// Gets or sets the status that were set.
        /// </summary>
        [DataMember]
        public CorrespondenceStatusTypeAgencyExternal StatusType { get; set; }
    }

    /// <summary>
    /// Represents details about a notification.
    /// </summary>
    [DataContract(Name = "Notification", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2013/11")]
    public class NotificationDetailsExternalBE
    {
        /// <summary>
        /// Gets or sets the recipient of the notification.
        /// </summary>
        [DataMember]
        public string Recipient { get; set; }

        /// <summary>
        /// Gets or sets the date for when the notification was sent to the recipient. 
        /// </summary>
        /// <remarks>
        /// If the field is empty (null), it means that the notification has not been sent yet.
        /// </remarks>
        [DataMember]
        public DateTime? SentDate { get; set; }

        /// <summary>
        /// Gets or sets the type of transport the notification was sent on.
        /// </summary>
        [DataMember]
        public TransportTypeExternal TransportType { get; set; }
    }
}
