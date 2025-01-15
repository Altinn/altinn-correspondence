using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceAttachmentMapper
{
    internal static CorrespondenceAttachmentEntity MapToEntity(InitializeCorrespondenceAttachmentExt initializeAttachmentExt, string resourceId, string sender)
    {
        string dataType;
        
        if (string.IsNullOrEmpty(initializeAttachmentExt.FileName))
        {
            throw new ArgumentException("FileName cannot be null or empty", nameof(initializeAttachmentExt));
        }
        
        string fileName = initializeAttachmentExt.FileName;
        string fileExtension = Path.GetExtension(fileName).ToLower();
        DataType = fileExtension switch
        {
            ".pdf" => "PDF",
            ".doc" => "WordDocument",
            ".docx" => "WordDocument",
            ".xls" => "ExcelDocument",
            ".xlsx" => "ExcelDocument",
            ".jpg" => "Image",
            ".jpeg" => "Image",
            ".png" => "Image",
            ".txt" => "Text",
            _ => "Unknown",
        };
        return new CorrespondenceAttachmentEntity
        {
            Created = DateTimeOffset.UtcNow,
            ExpirationTime = initializeAttachmentExt.ExpirationTime,
            Attachment = new AttachmentEntity
            {
                Created = DateTimeOffset.UtcNow,
                FileName = initializeAttachmentExt.FileName,
                Name = initializeAttachmentExt.Name,
                ResourceId = resourceId,
                Sender = sender,
                SendersReference = initializeAttachmentExt.SendersReference,
                Checksum = initializeAttachmentExt.Checksum,
                IsEncrypted = initializeAttachmentExt.IsEncrypted,
                DataLocationType = (AttachmentDataLocationType)initializeAttachmentExt.DataLocationType,
            }
        };
    }
}
