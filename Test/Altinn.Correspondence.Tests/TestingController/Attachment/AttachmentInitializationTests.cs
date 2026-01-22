using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using System.Net.Http.Json;
using System.Net;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Application;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class AttachmentInitializationTests : AttachmentTestBase
    {
        public AttachmentInitializationTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }
        [Fact]
        public async Task InitializeAttachment()
        {
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
            Assert.NotNull(attachmentId);
        }
        [Theory]
        [InlineData("rsietris//rsitersn")]
        [InlineData("    ")]
        public async Task InitializeAttachment_InvalidFilename_ReturnsBadRequest(string fileName)
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName(fileName)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeAttachment_FileNameTooLong_ReturnsBadRequest()
        {
            string namewith300chars = new string('a', 300);
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName(namewith300chars)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeAttachment_DisplayFileNameTooLong_ReturnsBadRequest()
        {
            string namewith300chars = new string('a', 300);
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithDisplayName(namewith300chars)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeAttachment_DisplayFileNameIsNull_Returns_OK()
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithDisplayName(null)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_AsRecipient_ReturnsForbidden()
        {
            var attachment = new AttachmentBuilder().CreateAttachment().Build();
            var initializeAttachmentResponse = await _recipientClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.Forbidden, initializeAttachmentResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_As_Different_Sender_As_Token_ReturnsUnauthorized()
        {
            var attachment = new AttachmentBuilder().CreateAttachment().Build();
            var initializeAttachmentResponse = await _wrongSenderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.Unauthorized, initializeAttachmentResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_WithExpirationBeforeMinimum_ReturnsBadRequest()
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithExpirationInDays(0)
                .Build();
            
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_WithExpirationAtOrAfterMinimum_ReturnsOk()
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithExpirationInDays(2)
                .Build();
            
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
        }

        [Theory]
        [InlineData('*')]
        [InlineData('?')]
        [InlineData('<')]
        [InlineData('>')]
        [InlineData('|')]
        [InlineData(':')]
        [InlineData('"')]
        [InlineData('\\')]
        [InlineData('/')]
        [InlineData('\0')]
        public async Task InitializeAttachment_WithIllegalCharactersInFileName_ReturnsBadRequest(char illegalChar)
        {
            var fileName = $"file{illegalChar}name.txt";
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName(fileName)
                .Build();
            
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            var responseContent = await initializeAttachmentResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
            Assert.Contains("Filename contains invalid characters", responseContent);
        }
        
        [Theory]
        [InlineData("CON")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("LPT1")]
        [InlineData("COM2")]
        public async Task InitializeAttachment_WithWindowsReservedFilename_ReturnsBadRequest(string reservedFileName)
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName(reservedFileName + ".txt")
                .Build();
            
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            var responseContent = await initializeAttachmentResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
            Assert.Contains(AttachmentErrors.FilenameInvalid.Message, responseContent);
        }

        [Theory]
        [InlineData("CONNECTION")]
        [InlineData("NULLABLE")]
        [InlineData("AUXILIARY")]
        [InlineData("COMPUTER")]
        [InlineData("FOONULBAR")]
        [InlineData("NO_CON")]

        public async Task InitializeAttachment_WithWindowsReservedFilenameAsPartOfName_ShouldSucceed(string filename)
        {
            
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName("my_" + filename + "_file.txt")
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
        }

        [Theory]
        [InlineData("trailingSpace ")]
        [InlineData("trailingDot.")]
        public async Task InitializeAttachment_WithIllegalFilenames_ReturnsBadRequest(string fileName)
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName(fileName + ".txt")
                .Build();
            
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            var responseContent = await initializeAttachmentResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
            Assert.Contains(AttachmentErrors.FilenameInvalid.Message, responseContent);
        }
    }
}
