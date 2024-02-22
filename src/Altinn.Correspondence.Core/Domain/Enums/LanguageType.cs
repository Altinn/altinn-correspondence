namespace Altinn.Correspondence.Core.Models.Enums
{
    /// <summary>
    /// Enumeration Providing the Supported Languages
    /// </summary>
    public enum LanguageType : int
    {
        /// <summary>
        /// Default language
        /// </summary>
        Default = 0, 

        /// <summary>
        /// There has been no changes to this section
        /// of the parameters since the edition was created
        /// </summary>
        English = 1033, 

        /// <summary>
        /// There has been changes to this section of parameters since the edition was
        /// created, but the section has required parameters that are not yet filled out.
        /// </summary>
        NorwegianNO = 1044, 

        /// <summary>
        /// All required parameters in this section has been filled out
        /// </summary>
        NorwegianNN = 2068, 

        /// <summary>
        /// All required parameters in this section has been filled out
        /// </summary>
        Sami = 1083, 
    }
}