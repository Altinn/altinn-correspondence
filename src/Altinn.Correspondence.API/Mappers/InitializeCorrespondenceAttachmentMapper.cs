using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceAttachmentMapper
{
    internal static CorrespondenceAttachmentEntity MapToEntity(InitializeCorrespondenceAttachmentExt initializeAttachmentExt, string resourceId, string sender)
    {
        string DataType;

        string fileName = initializeAttachmentExt.FileName; // Assuming FileName contains the full filename
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
                DataType = DataType,
                Checksum = initializeAttachmentExt.Checksum,
                IsEncrypted = initializeAttachmentExt.IsEncrypted,
                DataLocationType = (AttachmentDataLocationType)initializeAttachmentExt.DataLocationType,
            }
        };
    }
}
