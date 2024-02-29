namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines the different options for where a new Correspondence should be handled.
    /// </summary>
    public enum SdpSettingExternal
    {
        /// <summary>
        /// Specifies that the correspondence should be forwarded to a digital mail box.
        /// </summary>
        /// <remarks>
        /// Altinn will try to find the digital mail address of the reportee and forward the message. InsertCorrespondence will fail
        /// if no digital mail address exist.
        /// </remarks>
        ForwardOnly = 0,

        /// <summary>
        /// Specifies that the correspondence should be both made available in Altinn and forwarded to the digital mail box of the user.
        /// </summary>
        /// <remarks>
        /// Altinn will try to find the digital mail address of the reportee and forward the message inn addition to keeping a copy that
        /// is made available to the reportee. InsertCorrespondence will NOT fail if no digital mail address exist. Instead the forwarding
        /// will be skipped. The Receipt will contain a message with the results.
        /// </remarks>
        CopyAltinn = 1
    }
}
