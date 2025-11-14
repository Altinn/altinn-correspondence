import http from 'k6/http';
import { check, sleep } from 'k6';
import { getSenderAltinnToken, getRecipientAltinnToken } from './helpers/altinnTokenService.js';
import { buildInitializeCorrespondenceWithNewAttachmentPayload } from './helpers/correspondencePayloadBuilder.js';
import { cleanupBruksmonsterTestData } from './helpers/cleanupUseCaseTestsData.js';

const baseUrl = __ENV.base_url;
const resourceId = __ENV.resource_id;
const isProduction = (__ENV.base_url.toLowerCase().includes('platform.altinn.no')) ? true : false;
const ATTACHMENT_PATH = './fixtures/attachment.txt';
const ATTACHMENT_MIME = 'text/plain';
const ATTACHMENT_FILENAME = 'usecase-attachment.txt';
const ATTACHMENT_FILE_BIN = open(ATTACHMENT_PATH, 'b');

export const options = {
    thresholds: {
        checks: ["rate==1"],  // 100% of checks must pass
    },
};

/**
 * TC01: Initialize correspondence with a new attachment
 * TC02: Poll as recipient until the correspondence has status published
 * TC03: Retrieve attachment overview as sender
 * TC04: Download the correspondence attachment as recipient
 * TC05: Purge correspondence as recipient
 * cleanup: deletes data created by bruksmonster tests
 */
export default async function () {
    const { correspondenceId, attachmentId } = await TC01_InitializeCorrespondenceWithAttachment();
    await TC02_GetCorrespondencePublishedAsRecipient(correspondenceId);
    await TC03_GetAttachmentOverviewAsSender(attachmentId);
    await TC04_DownloadCorrespondenceAttachmentAsRecipient(correspondenceId, attachmentId);
    await TC05_PurgeCorrespondenceAsRecipient(correspondenceId);
    sleep(10); //give time for the purge to complete (Report purge activity to dialogporten might throw an error if it's not completed yet on cleanup)
    await cleanupBruksmonsterTestData();
}

async function TC01_InitializeCorrespondenceWithAttachment() {
    const token = await getSenderAltinnToken();
    check(token, { 'Sender altinn token obtained for initialize correspondence': t => typeof t === 'string' && t.length > 0 });

    const recipient = isProduction ? 'insert production recipient here' : '26818099002';

    const formBody = buildInitializeCorrespondenceWithNewAttachmentPayload(resourceId, recipient, ATTACHMENT_FILE_BIN, ATTACHMENT_FILENAME, ATTACHMENT_MIME);

    const headers = {
        Authorization: `Bearer ${token}`
    };

    const res = http.post(`${baseUrl}/correspondence/api/v1/correspondence/upload`, formBody, { headers });
    check(res, { 'Initialize correspondence with new attachment response status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`Initialize correspondence with new attachment failed. Status: ${res.status}. Body: ${res.body}`);
        return { correspondenceId: null, attachmentId: null };
    }

    let correspondenceId = null;
    let attachmentId = null;
    try {
        const payload = res.json();
        if (payload && payload.correspondences && payload.correspondences.length > 0) {
            correspondenceId = payload.correspondences[0].correspondenceId;
        }
		if (payload && payload.attachmentIds && payload.attachmentIds.length > 0) {
			attachmentId = payload.attachmentIds[0];
		}
		check(attachmentId, { 'Captured attachmentId from initialize response': id => typeof id === 'string' && id.length > 0 });
        check(correspondenceId, { 'Captured correspondenceId from initialize response': id => typeof id === 'string' && id.length > 0 });
    } catch (e) {
        console.error(`Failed parsing initialize response JSON. Error: ${e?.message}`);
    }

	console.log(`TC01: Test case completed`);
	return { correspondenceId, attachmentId };
}

async function TC02_GetCorrespondencePublishedAsRecipient(correspondenceId) {
    if (!correspondenceId) {
        console.error('TC02 aborted: No correspondenceId from TC01.');
        return;
    }

    const recipientToken = await getRecipientAltinnToken();
    check(recipientToken, { 'Recipient altinn token obtained for polling correspondence status': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${recipientToken}`
    };

    const isPublishedValue = (val) => {
        if (val === 2) return true;
        if (typeof val === 'string' && val.toLowerCase() === 'published') return true;
        return false;
    };

    const maxIterations = 10;
    let published = false;
    for (let i = 0; i < maxIterations; i++) {
        sleep(3);
        const r = http.get(`${baseUrl}/correspondence/api/v1/correspondence/${correspondenceId}`, { headers });
        if (r.status === 200) {
            const overview = r.json();
            const statusIsPublished = isPublishedValue(overview && overview.status);
            if (statusIsPublished) {
                published = true;
                break;
            }
            else {
                console.log(`TC02: Correspondence status is not published (status=${overview && overview.status}). Attempt ${i + 1}/${maxIterations}`);
            }
        }
		else if (r.status === 404) {
			console.log(`TC02:Correspondence overview not available yet (404). Attempt ${i + 1}/${maxIterations}`);
		}
        else {
            console.error(`Failed to get correspondence overview. Status: ${r.status}. Body: ${r.body}`);
        }
    }

    check(published, { 'Correspondence reached Published status within 30s': v => v === true });
    console.log(`TC02: Test case completed`);
}

async function TC03_GetAttachmentOverviewAsSender(attachmentId) {
	if (!attachmentId) {
		console.error('TC03 aborted: No attachmentId from TC01.');
		return;
	}

	const token = await getSenderAltinnToken();
	check(token, { 'Sender Altinn token obtained for attachment overview': t => typeof t === 'string' && t.length > 0 });

	const headers = {
		Authorization: `Bearer ${token}`
	};

	const r = http.get(`${baseUrl}/correspondence/api/v1/attachment/${attachmentId}`, { headers });
	check(r, { 'Attachment overview status 200': resp => resp.status === 200 });

	if (r.status === 200) {
		let bodyParsed = null;
		try {
			bodyParsed = r.json();
		} catch (e) {
			console.error(`TC03: Response was not valid JSON. Body: ${r.body}`);
		}
		check(bodyParsed, { 'Attachment overview has JSON body': b => b && typeof b === 'object' });
	}

	console.log('TC03: Test case completed');
}

async function TC04_DownloadCorrespondenceAttachmentAsRecipient(correspondenceId, attachmentId) {
    if (!attachmentId || !correspondenceId) {
		console.error('TC04 aborted: No attachmentId or correspondenceId from TC01.');
		return;
	}

	const recipientToken = await getRecipientAltinnToken();
	check(recipientToken, { 'Recipient Altinn token obtained for attachment download': t => typeof t === 'string' && t.length > 0 });

	const originalBytes = new Uint8Array(ATTACHMENT_FILE_BIN);

    let downloadedBytes = null;

    const headers = { Authorization: `Bearer ${recipientToken}` };
    const r = http.get(`${baseUrl}/correspondence/api/v1/correspondence/${correspondenceId}/attachment/${attachmentId}/download`, { headers, responseType: 'binary' });
    if (r.status === 200) {
        downloadedBytes = new Uint8Array(r.body);
    }
    else if (r.status === 404) {
        console.log(`TC04: Recipient correspondence download not found (404).`);
    } else {
        console.log(`TC04: Recipient correspondence download returned ${r.status}.`);
    }

	check(downloadedBytes, { 'Attachment downloaded successfully': b => b instanceof Uint8Array });
	if (!downloadedBytes) {
		console.error('TC04: Could not download attachment');
		return;
	}

	const sameLength = originalBytes.byteLength === downloadedBytes.byteLength;
	check(sameLength, { 'Attachment download: same byte length as uploaded': v => v === true });

	let sameContent = sameLength;
	if (sameLength) {
		for (let i = 0; i < originalBytes.byteLength; i++) {
			if (originalBytes[i] !== downloadedBytes[i]) {
				sameContent = false;
				break;
			}
		}
	}
	if (!sameContent) {
		console.error(`TC04: Downloaded content differs (len uploaded=${originalBytes.byteLength}, len downloaded=${downloadedBytes.byteLength})`);
	}
	check(sameContent, { 'Attachment download: content identical to uploaded file': v => v === true });

	console.log('TC04: Test case completed');
}

async function TC05_PurgeCorrespondenceAsRecipient(correspondenceId) {
	if (!correspondenceId) {
		console.error('TC05 aborted: No correspondenceId from TC01.');
		return;
	}

	const recipientToken = await getRecipientAltinnToken();
	check(recipientToken, { 'Recipient Altinn token obtained for purge': t => typeof t === 'string' && t.length > 0 });

	const headers = {
		Authorization: `Bearer ${recipientToken}`
	};

	const res = http.del(`${baseUrl}/correspondence/api/v1/correspondence/${correspondenceId}/purge`, null, { headers });
	check(res, { 'Purge correspondence status 200': r => r.status === 200 });

	if (res.status !== 200) {
		console.error(`TC05: Purge failed. Status: ${res.status}. Body: ${res.body}`);
		return;
	}

	const verify = http.get(`${baseUrl}/correspondence/api/v1/correspondence/${correspondenceId}`, { headers });
	if (verify.status !== 404) {
		console.error(`TC05: Post-purge GET did not return 404 (got ${verify.status}).`);
	}

    check(verify, { 'Get correspondence overview status 404 after purge': r => r.status === 404 });

	console.log('TC05: Test case completed');
}