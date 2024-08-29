namespace Altinn.Correspondence.Core.Services.Enums;

public enum AltinnEventType
{
    AttachmentInitialized,
    AttachmentUploadProcessing,
    AttachmentPublished,
    AttachmentUploadFailed,
    AttachmentPurged,
    AttachmentDownloaded,

    CorrespondenceInitialized,
    CorrespondencePublished,
    CorrespondenceArchived,
    CorrespondencePurged,
    CorrespondencePublishFailed,

    CorrespondenceReceiverRead,
    CorrespondenceReceiverConfirmed,
    CorrespondenceReceiverReplied,
    CorrespondenceReceiverNeverConfirmed,
    CorrespondenceReceiverReserved,
    CorrespondenceReceiverNeverRead,
}
