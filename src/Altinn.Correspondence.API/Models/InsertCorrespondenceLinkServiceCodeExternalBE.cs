namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a reply option of the type service.
    /// </summary>
    public class InsertCorrespondenceLinkServiceCodeExternalBE
    {
        /// <summary>
        /// Gets or sets the primary service id of the service to be used as a response.
        /// </summary>
        /// <remarks>
        /// To uniquely identify a service you also need the service edition code.
        /// </remarks>
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets the secondary service id of the service to be used as a response.
        /// </summary>
        /// <remarks>
        /// To uniquely identify a service you also need the service code.
        /// </remarks>
        public string ServiceEdition { get; set; }
    }
}