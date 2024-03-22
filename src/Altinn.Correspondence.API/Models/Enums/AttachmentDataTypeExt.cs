namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines the type of attachment.
    /// </summary>
    public enum AttachmentDataTypeExt : int
    {
        /// <summary>
        /// Specifies no attachment.
        /// </summary>
        application_None = 0, 

        /// <summary>
        /// Specifies PDF (Portable Document Format) attachment.
        /// </summary>
        application_pdf = 1, 

        /// <summary>
        /// Specifies Microsoft Office Word type attachment.
        /// </summary>
        application_msword = 2, 

        /// <summary>
        /// Specifies Microsoft Office Excel type attachment.
        /// </summary>
        application_vnd_ms_excel = 3, 

        /// <summary>
        /// Specifies Oasis OpenDocument text document type attachment.
        /// </summary>
        application_vnd_oasis_opendocument_text = 4, 

        /// <summary>
        /// Specifies Oasis OpenDocument presentation type attachment.
        /// </summary>
        application_vnd_oasis_opendocument_presentation = 5, 

        /// <summary>
        /// Specifies Oasis OpenDocument spreadsheet type attachment.
        /// </summary>
        application_vnd_oasis_opendocument_spreadsheet = 6, 

        /// <summary>
        /// Specifies RTF (Rich Text Format) type attachment.
        /// </summary>
        application_rtf = 7, 

        /// <summary>
        /// Specifies Microsoft Office PowerPoint type attachment.
        /// </summary>
        application_vnd_ms_powerpoint = 8, 

        /// <summary>
        /// Specifies post script type attachment.
        /// </summary>
        application_postscript = 9, 

        /// <summary>
        /// Specifies Zip type attachment.
        /// </summary>
        application_zip = 10, 

        /// <summary>
        /// Specifies plain text type attachment.
        /// </summary>
        text_plain = 11, 

        /// <summary>
        /// Specifies html type attachment.
        /// </summary>
        text_html = 12, 

        /// <summary>
        /// Specifies XML type attachment..
        /// </summary>
        text_xml = 13, 

        /// <summary>
        /// Specifies RTF (Rich Text Format) type attachment.
        /// </summary>
        text_rtf = 14, 

        /// <summary>
        /// Specifies rich text type attachment.
        /// </summary>
        text_richtext = 15, 

        /// <summary>
        /// Specifies binary type attachment.
        /// </summary>
        binary_octet_stream = 16, 

        /// <summary>
        /// Specifies no type attachment.
        /// </summary>
        not_Applicable = 17, 

        /// <summary>
        /// Specifies MTOM (Message Transmission Optimization Mechanism) encoded attachment.
        /// </summary>
        MTOM = 18, 

        /// <summary>
        /// Specifies Base64 encoded attachment.
        /// </summary>
        BASE64 = 19, 

        /// <summary>
        /// Specifies jpeg image type attachment.
        /// </summary>
        image_jpeg = 20, 

        /// <summary>
        /// Specifies gif image type attachment.
        /// </summary>
        image_gif = 21, 

        /// <summary>
        /// Specifies bmp image type attachment.
        /// </summary>
        image_bmp = 22
    }
}