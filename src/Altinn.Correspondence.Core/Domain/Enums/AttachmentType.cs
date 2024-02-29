namespace Altinn.Correspondence.Core.Domain.Models.Enums
{
    /// <summary>
    ///  Attachment type is used to provide information about the type of the attachment.
    /// </summary>
    public enum AttachmentType : int
    {
        /// <summary>
        /// When there is no attachment.
        /// </summary>
        application_None = 0,

        /// <summary>
        /// When the attachment is of PDF type.
        /// </summary>
        application_pdf = 1,

        /// <summary>
        /// When the attachment is of Microsoft word type.
        /// </summary>
        application_msword = 2,

        /// <summary>
        /// When the attachment is of Microsoft excel type.
        /// </summary>
        application_vnd_ms_excel = 3,

        /// <summary>
        /// When the attachment is of Open document of Text type.
        /// </summary>
        application_vnd_oasis_opendocument_text = 4,

        /// <summary>
        /// When the attachment is of Open document of Presentation type.
        /// </summary>
        application_vnd_oasis_opendocument_presentation = 5,

        /// <summary>
        /// When the attachment is of Open document of Spreadsheet type.
        /// </summary>
        application_vnd_oasis_opendocument_spreadsheet = 6,

        /// <summary>
        /// When the attachment is of Rich text format type.
        /// </summary>
        application_rtf = 7,

        /// <summary>
        ///  When the attachment is of Microsoft power point type.
        /// </summary>
        application_vnd_ms_powerpoint = 8,

        /// <summary>
        /// When the attachment is of Postscript type.
        /// </summary>
        application_postscript = 9,

        /// <summary>
        /// When the attachment is a zip type.
        /// </summary>
        application_zip = 10,

        /// <summary>
        /// When the attachment is a plain text.
        /// </summary>
        text_plain = 11,

        /// <summary>
        /// When the attachment is a html text.
        /// </summary>
        text_html = 12,

        /// <summary>
        /// When the attachment is a xml text.
        /// </summary>
        text_xml = 13,

        /// <summary>
        /// When the attachment is of rich text format type.
        /// </summary>
        text_rtf = 14,

        /// <summary>
        ///  When the attachment is a rich text.
        /// </summary>
        text_richtext = 15,

        /// <summary>
        ///  When the attachment is a binary.
        /// </summary>
        binary_octet_stream = 16,

        /// <summary>
        ///  When the attachment is not of any type.
        /// </summary>
        not_Applicable = 17,

        /// <summary>
        /// MTOM type
        /// </summary>
        MTOM = 18,

        /// <summary>
        /// BASE64 type
        /// </summary>
        BASE64 = 19,

        /// <summary>
        /// JPEG type
        /// </summary>
        image_jpeg = 20,

        /// <summary>
        /// GIF type
        /// </summary>
        image_gif = 21,

        /// <summary>
        /// Image type
        /// </summary>
        image_bmp = 22,

        /// <summary>
        /// Form Task PDF
        /// </summary>
        formtask_pdf = 23
    }
}