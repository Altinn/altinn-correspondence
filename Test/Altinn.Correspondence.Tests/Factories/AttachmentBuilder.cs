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
                Name = "Test file logical name",
                Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
                SendersReference = "1234",
                FileName = "test-file.txt",
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