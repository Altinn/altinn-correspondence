using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;
using Org.BouncyCastle.Crypto.Utilities;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class SyncCorrespondenceStatusEventHandlerTests
    {
        private readonly Mock<IAltinnAuthorizationService> _altinnAuthorizationServiceMock;
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock;
        private readonly Mock<IAttachmentStatusRepository> _attachmentStatusRepositoryMock;
        private readonly Mock<IStorageRepository> _storageRepositoryMock;
        private readonly Mock<IDialogportenService> _dialogPortenServiceMock;
        private readonly Mock<ILogger<SyncCorrespondenceStatusEventHandler>> _loggerMock;
        private readonly SyncCorrespondenceStatusEventHandler _handler;
        private readonly UpdateCorrespondenceStatusHelper _updateHelper;
        private readonly SyncCorrespondenceStatusEventHelper _syncHelper;
        private readonly PurgeCorrespondenceHelper _purgeHelper;

        public SyncCorrespondenceStatusEventHandlerTests()
        {
            _altinnAuthorizationServiceMock = new Mock<IAltinnAuthorizationService>();
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _attachmentRepositoryMock = new Mock<IAttachmentRepository>();
            _attachmentStatusRepositoryMock = new Mock<IAttachmentStatusRepository>();
            _storageRepositoryMock = new Mock<IStorageRepository>();
            _dialogPortenServiceMock = new Mock<IDialogportenService>();
            _loggerMock = new Mock<ILogger<SyncCorrespondenceStatusEventHandler>>();

            _handler = new SyncCorrespondenceStatusEventHandler(_altinnAuthorizationServiceMock.Object,
                _altinnRegisterServiceMock.Object,
                _correspondenceRepositoryMock.Object,
                _updateHelper = new UpdateCorrespondenceStatusHelper(_backgroundJobClientMock.Object, _correspondenceStatusRepositoryMock.Object),
                _purgeHelper = new PurgeCorrespondenceHelper(_attachmentRepositoryMock.Object, _storageRepositoryMock.Object, _attachmentStatusRepositoryMock.Object, _correspondenceRepositoryMock.Object, _correspondenceStatusRepositoryMock.Object, _dialogPortenServiceMock.Object, _backgroundJobClientMock.Object),                
                _syncHelper = new SyncCorrespondenceStatusEventHelper(
                    _correspondenceStatusRepositoryMock.Object,
                    _dialogPortenServiceMock.Object,
                    _backgroundJobClientMock.Object,
                    _purgeHelper),
                _loggerMock.Object);
        }

    }
} 