function generateRandomData(sizeInBytes) {
    const data = new Uint8Array(sizeInBytes);
    for (let i = 0; i < sizeInBytes; i++) {
        data[i] = Math.floor(Math.random() * 256); // Random byte (0-255)
    }
    return data;
}

function stringToUint8Array(str) {
    const arr = new Uint8Array(str.length);
    for (let i = 0; i < str.length; i++) {
        arr[i] = str.charCodeAt(i);
    }
    return arr;
}

export function getCorrespondenceForm(resource_id, sender, recipient, boundary) {
    const CRLF = '\r\n';
    let body = '';
    
    function addFormField(name, value) {
        body += `--${boundary}${CRLF}`;
        body += `Content-Disposition: form-data; name="${name}"${CRLF}${CRLF}`;
        body += `${value}${CRLF}`;
    }

    const now = new Date(); 
    const visibleFrom = new Date();
    const dueDateTime = new Date(now.setMonth(now.getMonth() + 1));
    const deleteAfter = new Date(now.setMonth(now.getMonth() + 4));
    
    addFormField('Recipients[0]', recipient);
    addFormField('Correspondence.ResourceId', resource_id);
    addFormField('Correspondence.Sender', '0192:' + sender);
    addFormField('Correspondence.sendersReference', '1234');
    addFormField('Correspondence.content.language', 'en');
    addFormField('Correspondence.content.messageTitle', 'Test');
    addFormField('Correspondence.content.messageSummary', 'Test');
    addFormField('Correspondence.content.messageBody', 'Test');
    addFormField('Correspondence.content.attachments[0].DataLocationType', 'ExistingExternalStorage');
    addFormField('Correspondence.content.attachments[0].Name', 'testfile.txt');
    addFormField('Correspondence.content.attachments[0].Sender', '0192:' + sender);
    addFormField('Correspondence.content.attachments[0].SendersReference', '1234');
    addFormField('Correspondence.content.attachments[0].FileName', 'testfile.txt');
    addFormField('Correspondence.content.attachments[0].IsEncrypted', 'true');
    addFormField('Correspondence.visibleFrom', visibleFrom.toISOString());
    addFormField('Correspondence.allowSystemDeleteAfter', deleteAfter.toISOString());
    addFormField('Correspondence.dueDateTime', dueDateTime.toISOString());
    addFormField('Correspondence.content.externalReferences[0].referenceValue', 'test');
    addFormField('Correspondence.content.externalReferences[0].referenceType', 'AltinnBrokerFileTransfer');
    addFormField('Correspondence.content.propertyList.deserunt_12', 'string');
    addFormField('Correspondence.content.propertyList.culpa_852', 'string');
    addFormField('Correspondence.content.propertyList.anim5', 'string');
    addFormField('Correspondence.content.notification.notificationTemplate', '1');
    addFormField('Correspondence.content.notification.notificationChannel', '2');
    addFormField('Correspondence.content.notification.SendReminder', 'true');
    addFormField('Correspondence.content.notification.EmailBody', 'Test av varsling');
    addFormField('Correspondence.content.notification.EmailSubject', 'Innholdet i ett testvarsel');
    addFormField('Correspondence.content.notification.SmsBody', 'Dette er ett testvarsel');
    addFormField('Correspondence.content.notification.ReminderEmailSubject', 'Revarsling');
    addFormField('Correspondence.content.notification.ReminderEmailBody', 'Dette er revarsling av ett testvarsel');
    addFormField('Correspondence.content.notification.ReminderSmsBody', 'Dette er revarsel av ett test varsel');
    addFormField('Correspondence.content.notification.isReservable', 'true');

    body += `--${boundary}${CRLF}`;
    body += `Content-Disposition: form-data; name="Attachments"; filename="testfile.txt"${CRLF}`;
    body += `Content-Type: application/octet-stream${CRLF}${CRLF}`;
    
    let fileArray = new Uint8Array(generateRandomData(50 * 1024));
    
    let headerArray = stringToUint8Array(body);
    let footerArray = stringToUint8Array(`${CRLF}--${boundary}--${CRLF}`);
    
    let totalSize = headerArray.length + fileArray.length + footerArray.length;
    
    let finalArray = new Uint8Array(totalSize);
    finalArray.set(headerArray, 0);
    finalArray.set(fileArray, headerArray.length);
    finalArray.set(footerArray, headerArray.length + fileArray.length);
    
    return finalArray;
}