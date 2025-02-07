export function getCorrespondenceJson(resource_id, sender, recipient) {
    const now = new Date();
    const visibleFrom = new Date(now);
    const dueDateTime = new Date(now.getTime());
    dueDateTime.setMonth(dueDateTime.getMonth() + 1);
    const deleteAfter = new Date(now.getTime());
    deleteAfter.setMonth(deleteAfter.getMonth() + 5);
    const data_without_attachment = {
        Correspondence: {
            resourceId: resource_id,
            sender: "0192:" + sender,
            sendersReference: "1",
            content: {
                language: "nb",
                messageTitle: "Meldingstittel",
                messageSummary: "Ett sammendrag for meldingen",
                messageBody: "# meldingsteksten. Som kan være plain text eller markdown ",
                attachments: [],
            },
            visibleFrom: visibleFrom.toISOString(),
            allowSystemDeleteAfter: deleteAfter.toISOString(),
            dueDateTime: dueDateTime.toISOString(),
            externalReferences: [],
            propertyList: {},
            replyOptions: [
                {
                    linkURL: "https://www.test.no",
                    linkText: "test",
                },
                {
                    linkURL: "https://test.no",
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
        Recipients: [recipient],
        existingAttachments: [],
    }
    return JSON.stringify(data_without_attachment);
}