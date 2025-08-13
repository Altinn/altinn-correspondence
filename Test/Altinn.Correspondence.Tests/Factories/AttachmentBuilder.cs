using System.Text;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondence.Tests.Factories
{
    public class AttachmentBuilder
    {
        private InitializeAttachmentExt _attachment;
        public InitializeAttachmentExt Build()
        {
            return _attachment;
        }
        public AttachmentBuilder CreateAttachment()
        {
            _attachment = new InitializeAttachmentExt()
            {
                ResourceId = "1",
                SendersReference = "1234",
                FileName = "test-file.txt",
                DisplayName = "Test file",
                IsEncrypted = false
            };
            return this;
        }
        public AttachmentBuilder WithSender(string sender)
        {
            _attachment.Sender = sender;
            return this;
        }
        public AttachmentBuilder WithFileName(string fileName)
        {
            _attachment.FileName = fileName;
            return this;
        }
        public AttachmentBuilder WithDisplayName(string? displayName)
        {
            _attachment.DisplayName = displayName;
            return this;
        }
        public AttachmentBuilder WithChecksum(byte[] byteData)
        {
            var checksum = AttachmentHelper.CalculateChecksum(byteData);
            _attachment.Checksum = checksum;
            return this;
        }
        public AttachmentBuilder WithChecksum(string data)
        {
            var byteData = Encoding.UTF8.GetBytes(data);
            var checksum = AttachmentHelper.CalculateChecksum(byteData);
            _attachment.Checksum = checksum;
            return this;
        }
    }
}