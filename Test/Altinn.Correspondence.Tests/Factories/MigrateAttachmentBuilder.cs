using System.Text;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondence.Tests.Factories
{
    public class MigrateAttachmentBuilder
    {
        public required MigrateInitializeAttachmentExt _attachment;
        public MigrateInitializeAttachmentExt Build()
        {
            return _attachment;
        }
        public MigrateAttachmentBuilder CreateAttachment()
        {
            _attachment = new MigrateInitializeAttachmentExt()
            {
                ResourceId = "1",
                Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
                SendersReference = "1234",
                FileName = "test-file.txt",
                DisplayName = "Test file",
                IsEncrypted = false,
                SenderPartyUuid = new Guid("EBF0DA78-DB7C-4087-9711-2C64DB201EB1"),
                Altinn2AttachmentId = "R1",
                Created = new DateTimeOffset(new DateTime(2025, 5, 1))
            };
            return this;
        }
        public MigrateAttachmentBuilder WithSender(string sender)
        {
            _attachment.Sender = sender;
            return this;
        }
        public MigrateAttachmentBuilder WithFileName(string fileName)
        {
            _attachment.FileName = fileName;
            return this;
        }
        public MigrateAttachmentBuilder WithDisplayName(string? displayName)
        {
            _attachment.DisplayName = displayName;
            return this;
        }
        public MigrateAttachmentBuilder WithChecksum(byte[] byteData)
        {
            var checksum = AttachmentHelper.CalculateChecksum(byteData);
            _attachment.Checksum = checksum;
            return this;
        }
        public MigrateAttachmentBuilder WithChecksum(string data)
        {
            var byteData = Encoding.UTF8.GetBytes(data);
            var checksum = AttachmentHelper.CalculateChecksum(byteData);
            _attachment.Checksum = checksum;
            return this;
        }
        public MigrateAttachmentBuilder WithSenderPartyUuid(Guid sender)
        {
            _attachment.SenderPartyUuid = sender;
            return this;
        }
    }
}