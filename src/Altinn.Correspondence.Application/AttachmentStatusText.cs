namespace Altinn.Correspondence.Application
{
    public static class AttachmentStatusText
    {
        public const string InvalidLocationUrl = "Could not get data location url";
        public const string ChecksumMismatch = "Checksum mismatch";
        public const string UploadFailed = "Upload failed";
        public const string UploadTimedOut = "Upload request timed out due to data arriving too slowly";
        public const string UploadInterrupted = "Upload interrupted";
    }
}