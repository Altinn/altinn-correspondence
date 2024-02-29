#region Namespace imports

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

#endregion

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// The class holds counters for correspondences
    /// </summary>
    [Serializable]
    [DataContract(Namespace = "http://schemas.altinn.no/services/ServiceEngine/ReporteeElementList/2009/10")]
    public class CorrespondenceCountDetailsBE
    {
        #region Data contract members

        /// <summary>
        /// Gets or sets Counter for new correspondences which has not been opened
        /// </summary>
        public int NewCount { get; set; }

        /// <summary>
        /// Gets or sets Counter for the total amount of correspondences
        /// </summary>
        public int TotalCount { get; set; }

        #endregion
    }

    /// <summary>
    /// Collection of CorrespondenceCountDetailsBE
    /// </summary>
    [CollectionDataContract(Namespace = "http://schemas.altinn.no/services/ServiceEngine/ReporteeElementList/2009/10")]
    public class CorrespondenceCountDetailsBEList : List<CorrespondenceCountDetailsBE>
    {
    }
}