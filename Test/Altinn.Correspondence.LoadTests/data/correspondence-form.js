import http from 'k6/http';
import { FormData } from 'https://jslib.k6.io/formdata/0.0.2/index.js';

const file = open("./testfile.txt", "b");

export function getCorrespondenceForm(resource_id, sender, recipient) {
    const formData = new FormData();
    formData.append('Recipients[0]', recipient);
    formData.append('Correspondence.ResourceId', resource_id);
    formData.append('Correspondence.Sender', "0192:" +sender);
    formData.append('Correspondence.sendersReference', "1234");
    formData.append('Correspondence.content.language', "en");
    formData.append('Correspondence.content.messageTitle', "Test");
    formData.append('Correspondence.content.messageSummary', "Test");
    formData.append('Correspondence.content.messageBody', "Test");
    formData.append('Correspondence.content.attachments[0].DataLocationType', "ExistingExternalStorage");
    formData.append('Correspondence.content.attachments[0].DataType', "1");
    formData.append('Correspondence.content.attachments[0].Name', "testfile.txt");
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
    return formData;
}