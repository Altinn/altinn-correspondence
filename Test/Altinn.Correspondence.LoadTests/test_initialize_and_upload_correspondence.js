import http from 'k6/http';
import { sleep, check } from 'k6';
import { FormData } from 'https://jslib.k6.io/formdata/0.0.2/index.js';

export const options = {
    vus: 10,
    // iterations: 1,
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
const file = open("./data/testfile.txt", "b");

const formData = new FormData();
formData.append('Recipients[0]', "0192:123456789");
formData.append('Correspondence.ResourceId', resource_id);
formData.append('Correspondence.Sender', sender);
formData.append('Correspondence.sendersReference', "1234");
formData.append('Correspondence.content.language', "en");
formData.append('Correspondence.content.messageTitle', "Test");
formData.append('Correspondence.content.messageSummary', "Test");
formData.append('Correspondence.content.messageBody', "Test");
formData.append('Correspondence.content.attachments[0].DataLocationType', "ExisitingExternalStorage");
formData.append('Correspondence.content.attachments[0].DataType', "1");
formData.append('Correspondence.content.attachments[0].Name', "testfile.txt");
formData.append('Correspondence.content.attachments[0].RestrictionName', "testfile.txt");
formData.append('Correspondence.content.attachments[0].Sender', sender);
formData.append('Correspondence.content.attachments[0].SendersReference', "1234");
formData.append('Correspondence.content.attachments[0].FileName', "testfile.txt");
formData.append('Correspondence.content.attachments[0].IsEncrypted', "true");
formData.append('Correspondence.visibleFrom', "2024-05-29T13:31:28.290518+00:00");
formData.append('Correspondence.allowSystemDeleteAfter', "2025-05-29T13:31:28.290518+00:00");
formData.append('Correspondence.dueDateTime', "2025-05-29T13:31:28.290518+00:00");
formData.append('Correspondence.content.externalReferences[0].referenceValue', "test");
formData.append('Correspondence.content.externalReferences[0].referenceType', "AltinnBrokerFileTransfer");
formData.append('Correspondence.content.propertyList.deserunt_12', "string");
formData.append('Correspondence.content.propertyList.culpa_852', "string");
formData.append('Correspondence.content.propertyList.anim5', "string");
formData.append('Correspondence.content.replyOptions[0].linkURL', "www.dgidir.no");
formData.append('Correspondence.content.replyOptions[0].linkText', "dgidir");
formData.append('Correspondence.content.replyOptions[1].linkURL', "www.dgidir.no");
formData.append('Correspondence.content.replyOptions[1].linkText', "dgidir");
formData.append('Correspondence.content.notification.notificationTemplate', "1");
formData.append('Correspondence.content.notification.notificationChannel', "2");
formData.append('Correspondence.content.notification.SendReminder', "true");
formData.append('Correspondence.content.notification.EmailBody', "Test av varsling");
formData.append('Correspondence.content.notification.EmailSubject', "Innholdet i ett testvarsel");
formData.append('Correspondence.content.notification.SmsBody', "Dette er ett testvarsel");
formData.append('Correspondence.content.notification.ReminderEmailSubject', "Revarsling ");
formData.append('Correspondence.content.notification.ReminderEmailBody', "Dette er revarsling av ett testvarsel");
formData.append('Correspondence.content.notification.ReminderSmsBody', "Dette er revarsel av ett test varsel");
formData.append('Correspondence.content.notification.isReservable', "true");
formData.append('Attachments', http.file(file, 'testfile.txt', 'text/plain'));

export default async function() {
    let headers = {
        'Authorization': 'Bearer ' + TOKENS.DUMMY_SENDER_TOKEN,
        'Content-Type': 'multipart/form-data; boundary=' + formData.boundary,
        'Accept': '*/*, text/plain',
        'Accept-Encoding': 'gzip, deflate, br',
        'Connection': 'keep-alive'
    };
    var result = await http.asyncRequest('POST',
        `${BASE_URL}/correspondence/api/v1/correspondence/upload`,
        formData.body(), { headers: headers });

    var status = check(result, { 'Initialize: status was 200': (r) => r.status == 200 });
    checkResult(result, status);
}
