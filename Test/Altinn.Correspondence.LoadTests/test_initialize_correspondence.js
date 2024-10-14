import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
    vus: 1,
    duration: '10s',
}

function checkResult(res, status) {
    if (!status) {
        console.error(status)
        console.error(res)
    }
}
var TOKENS = {
    DUMMY_SENDER_TOKEN:
        "",
};
const BASE_URL = "http://localhost:5096";
const resource_id = "dagl-correspondence";
const sender = "0192:313154599";
const data_without_attachment = {
    Correspondence: {
        resourceId: resource_id,
        sender: sender,
        sendersReference: "1",
        content: {
            language: "nb",
            messageTitle: "Meldingstittel",
            messageSummary: "Ett sammendrag for meldingen",
            messageBody: "# meldingsteksten. Som kan være plain text eller markdown ",
            attachments: [],
        },
        visibleFrom: "2024-09-28T12:44:28.290518+00:00",
        allowSystemDeleteAfter: "2025-08-29T13:31:28.290518+00:00",
        dueDateTime: "2025-05-29T13:31:28.290518+00:00",
        externalReferences: [],
        propertyList: {},
        replyOptions: [
            {
                linkURL: "www.test.no",
                linkText: "test",
            },
            {
                linkURL: "test.no",
                linkText: "test",
            },
        ],
        notification: {
            notificationTemplate: 0,
            notificationChannel: 3,
            SendReminder: true,
            EmailBody: "Test av varsel",
            EmailSubject: "Dette er innholdet i ett varsel",
            SmsBody: "Dette er innholdet i ett testvarsel",
            ReminderEmailBody: "Dette er test av revarsling ",
            ReminderEmailSubject: "Test av revarsel",
            ReminderSmsBody: "Dette er en test av revarslingl",
        },
        isReservable: true,
    },
    Recipients: ["0192:123456789"],
    existingAttachments: [],
};

export default async function() {
    let headers = {
        'Authorization': 'Bearer ' + TOKENS.DUMMY_SENDER_TOKEN,
        'Content-Type': 'application/json',
        'Accept': '*/*, text/plain',
        'Accept-Encoding': 'gzip, deflate, br',
        'Connection': 'keep-alive'
    };
    var result = await http.asyncRequest('POST',
        `${BASE_URL}/correspondence/api/v1/correspondence`,
        JSON.stringify(data_without_attachment), { headers: headers });

    var status = check(result, { 'Initialize: status was 200': (r) => r.status == 200 });
    checkResult(result, status);
}
