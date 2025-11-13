import http from 'k6/http';
import { toUrn } from './commonUtils.js';

export function buildInitializeCorrespondencePayload(resourceId, ssnOrOrgno) {
    const recipient = toUrn(ssnOrOrgno);
    const nowRef = `usecase-${Date.now()}`;
    return {
        Correspondence: {
            resourceId,
            sendersReference: nowRef,
            content: {
                language: 'nb',
                messageTitle: 'Use case test title',
                messageSummary: 'Use case test summary',
                messageBody: 'Use case test body',
                attachments: []
            },
        },
        Recipients: [recipient].filter(Boolean),
        existingAttachments: []
    };
}

export function buildInitializeCorrespondenceWithNewAttachmentPayload(resourceId, ssnOrOrgno, attachmentFileBin, attachmentFileName, attachmentMime) {
    const recipient = toUrn(ssnOrOrgno);
    const nowRef = `usecase-${Date.now()}`;
    const attachmentRef = `usecase-attachment-${Date.now()}`;

    const form = {
        'request.Correspondence.ResourceId': resourceId,
        'request.Correspondence.SendersReference': nowRef,
        'request.Correspondence.Content.Language': 'nb',
        'request.Correspondence.Content.MessageTitle': 'Use case test title',
        'request.Correspondence.Content.MessageSummary': 'Use case test summary',
        'request.Correspondence.Content.MessageBody': 'Use case test body',
        'request.Correspondence.Content.Attachments[0].FileName': attachmentFileName,
        'request.Correspondence.Content.Attachments[0].DisplayName': attachmentFileName,
        'request.Correspondence.Content.Attachments[0].IsEncrypted': 'false',
        'request.Correspondence.Content.Attachments[0].SendersReference': attachmentRef,
        'request.Correspondence.Content.Attachments[0].DataLocationType': '0',
        'request.Recipients[0]': recipient,
        attachments: http.file(attachmentFileBin, attachmentFileName, attachmentMime)
    };

    return form;
}