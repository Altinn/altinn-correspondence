using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// Business entity representing details about a correspondence. The details included are
    /// related to the shipment type Correspondence Outbound and include data about the correspondence needed by the shipment.
    /// For example; at what time was the correspondence read for the first.
    /// </summary>
    [Serializable]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2014/10")]
    [XmlRootAttribute(Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2014/10", IsNullable = false)]
    public class CorrespondenceOutboundData
    {
        /// <summary>
        /// Gets or sets the internal altinn id of the correspondence. Also called reportee element id.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 0)]
        public int CorrespondenceId { get; set; }

        /// <summary>
        /// Gets or sets the reportee of the correspondence. 
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 1)]
        public string Reportee { get; set; }

        /// <summary>
        /// Gets or sets the external service code value of the service that the correspondence is associated with.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 2)]
        public string ExternalServiceCode { get; set; }

        /// <summary>
        /// Gets or sets the external service edition code value of the service that the correspondence is associated with.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 3)]
        public int ExternalServiceEditionCode { get; set; }

        /// <summary>
        /// Gets or sets the external system reference of the correspondence. 
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 4)]
        public string ExternalSystemReference { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the correspondence has been read or not.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 5)]
        public bool IsRead { get; set; }

        /// <summary>
        /// Gets or sets the date and time for when the correspondence was read the first time.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 6)]
        public DateTime ReadDateTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the correspondence has been confirmed or not.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 7)]
        public bool IsConfirmed { get; set; }

        /// <summary>
        /// Gets or sets the date and time for when the correspondence was confirmed the first time.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 8)]
        public DateTime ConfirmedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the SSN of the user that confirmed the correspondence. This is null if IsConfirmed is false.
        /// </summary>
        [XmlElementAttribute(Form = System.Xml.Schema.XmlSchemaForm.Unqualified, Order = 9)]
        public string ConfirmedBy { get; set; }
    }
}
