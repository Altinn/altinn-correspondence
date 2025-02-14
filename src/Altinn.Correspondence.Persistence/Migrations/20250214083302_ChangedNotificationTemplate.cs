using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangedNotificationTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE correspondence.\"NotificationTemplates\" SET " +
                "\"EmailBody\" = 'Hei. $recipientName$ har mottatt en ny melding fra $sendersName$. {textToken}Logg deg inn i Altinn for å se denne meldingen.', " +
                "\"EmailSubject\" = 'Melding mottatt i Altinn {textToken}', " +
                "\"ReminderEmailBody\" = 'Hei. Dette er en påminnelse om at $recipientName$ har mottatt en ny melding fra $sendersName$. {textToken}Logg deg inn i Altinn for å se denne meldingen.', " +
                "\"ReminderEmailSubject\" = 'Påminnelse - melding mottatt i Altinn {textToken}', " +
                "\"SmsBody\" = 'Hei. $recipientName$ har mottatt en ny melding fra $sendersName$. {textToken}Logg deg inn i Altinn for å se denne meldingen.' ," +
                "\"ReminderSmsBody\" = 'Hei. Dette er en påminnelse om at $recipientName$ har mottatt en ny melding fra $sendersName$. {textToken}Logg deg inn i Altinn for å se denne meldingen.' Where " +
                "\"Language\" = 'nb' and " +
                "\"Template\" = 1");


            migrationBuilder.Sql(
                "UPDATE correspondence.\"NotificationTemplates\" SET " +
                "\"EmailBody\" = 'Hello. $recipientName$ has received a new message from $sendersName$. {textToken}Log in to Altinn to see this message.', " +
                "\"EmailSubject\" = 'Message received in Altinn {textToken}', " +
                "\"ReminderEmailBody\" = 'Hello. This is a reminder that $recipientName$ has received a new message from $sendersName$. {textToken}Log in to Altinn to see this message.'," +
                "\"ReminderEmailSubject\" = 'Reminder - message received in Altinn {textToken}', " +
                "\"SmsBody\" = 'Hello. $recipientName$ has received a new message from $sendersName$. {textToken}Log in to Altinn to see this message.', " +
                "\"ReminderSmsBody\" = 'Hello. This is a reminder that $recipientName$ has received a new message from $sendersName$. {textToken}Log in to Altinn to see this message.' Where " +
                "\"Language\" = 'en' and " +
                "\"Template\" = 1");


            migrationBuilder.Sql(
                "UPDATE correspondence.\"NotificationTemplates\"  SET " +
                "\"EmailBody\" = 'Hei. $recipientName$ har motteke ei ny melding frå $sendersName$. {textToken}Logg deg inn i Altinn for å sjå denne meldinga.', " +
                "\"EmailSubject\" = 'Melding motteke i Altinn {textToken}', " +
                "\"ReminderEmailBody\" = 'Hei. Dette er ei påminning om at $recipientName$ har motteke ei ny melding frå $sendersName$. {textToken}Logg deg inn i Altinn for å sjå denne meldinga.', " +
                "\"ReminderEmailSubject\" = 'Påminning - melding motteke i Altinn {textToken}', " +
                "\"SmsBody\" = 'Hei. $recipientName$ har motteke ei ny melding frå $sendersName$. {textToken}Logg deg inn i Altinn for å sjå denne meldinga.', " +
                "\"ReminderSmsBody\" = 'Hei. Dette er ei påminning om at $recipientName$ har motteke ei ny melding frå $sendersName$. {textToken}Logg deg inn i Altinn for å sjå denne meldinga.' Where " +
                "\"Language\" = 'nn' and " +
                "\"Template\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
