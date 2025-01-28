export function getCorrespondenceJson(resource_id, sender, recipient) {
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
            visibleFrom: "2024-09-28T12:44:28.290518+00:00",
            allowSystemDeleteAfter: "2025-08-29T13:31:28.290518+00:00",
            dueDateTime: "2025-05-29T13:31:28.290518+00:00",
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