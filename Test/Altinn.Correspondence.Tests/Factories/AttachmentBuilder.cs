using System.Text;
using Altinn.Correspondence.API.Models;
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
                DataType = "html",
                Name = "Test file logical name",
                Sender = "0192:991825827",
                SendersReference = "1234",
                FileName = "test-file",
                IsEncrypted = false
            };
            return this;
        }
        public AttachmentBuilder WithSender(string sender)
        {
            _attachment.Sender = sender;
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