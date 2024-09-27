using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class add_Notification_templates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Template = table.Column<int>(type: "integer", nullable: false),
                    RecipientType = table.Column<int>(type: "integer", nullable: true),
                    EmailSubject = table.Column<string>(type: "text", nullable: false),
                    EmailBody = table.Column<string>(type: "text", nullable: false),
                    SmsBody = table.Column<string>(type: "text", nullable: false),
                    ReminderEmailBody = table.Column<string>(type: "text", nullable: false),
                    ReminderEmailSubject = table.Column<string>(type: "text", nullable: false),
                    ReminderSmsBody = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
           schema: "correspondence",
           table: "NotificationTemplates",
           columns: new[] { "EmailBody", "EmailSubject", "Language", "RecipientType", "ReminderEmailBody", "ReminderEmailSubject", "ReminderSmsBody", "SmsBody", "Template" },
           values: new object[,]
           {
                    { "{textToken}", "{textToken}", null, null, "{textToken}", "{textToken}", "{textToken}", "{textToken}", 0 },
                    { "Hei $recipientName$, du har mottatt en ny melding i Altinn fra $sendersName$. {textToken} Logg deg inn i Altinn inboks for å se denne meldingen.",
                    "Du har mottatt en melding i Altinn {textToken}", "nb", 0, "Hei $recipientName$, dette er en påminnelse om at du har mottatt en ny melding i Altinn fra $sendersName$. {textToken}Logg deg inn i Altinn inboks for å se denne meldingen.",
                    "Påminnelse - du har mottatt en melding i Altinn {textToken}", "Hei $recipientName$, dette er en påminnelse om at du har mottatt en ny melding i Altinn fra $sendersName$. {textToken} Logg deg inn i Altinn inboks for å se denne meldingen.",
                     "Hei $recipientName$, du har mottatt en ny melding i Altinn fra $sendersName$. {textToken}Logg deg inn i Altinn inboks for å se denne meldingen.", 1 },
                    { "Hei $recipientName$, du har mottatt en ny melding i Altinn fra $sendersName$. {textToken} Logg deg inn i Altinn inboks for å se denne meldingen.",
                    "Du har mottatt en melding i Altinn {textToken}", "nb", 1, "Hei $recipientName$, dette er en påminnelse om at du har mottatt en ny melding i Altinn fra $sendersName$. {textToken}Logg deg inn i Altinn inboks for å se denne meldingen.",
                    "Påminnelse - du har mottatt en melding i Altinn {textToken}", "Hei $recipientName$, dette er en påminnelse om at du har mottatt en ny melding i Altinn fra $sendersName$. {textToken}Logg deg inn i Altinn inboks for å se denne meldingen.",
                     "Hei $recipientName$, du har mottatt en ny melding i Altinn fra $sendersName$. {textToken}Logg deg inn i Altinn inboks for å se denne meldingen.", 1 },
                    
                    //english
                    { "Hello $recipientName$, you have received a new message in Altinn from $sendersName$. {textToken} Log in to Altinn inbox to see this message.",
                    "You have received a message in Altinn {textToken}", "en", 0, "Hello $recipientName$, this is a reminder that you have received a new message in Altinn from $sendersName$. {textToken}Log in to Altinn inbox to see this message.",
                    "Reminder - you have received a message in Altinn {textToken}", "Hello $recipientName$, this is a reminder that you have received a new message in Altinn from $sendersName$. {textToken} Log in to Altinn inbox to see this message.",
                     "Hello $recipientName$, you have received a new message in Altinn from $sendersName$. {textToken}Log in to Altinn inbox to see this message.", 1 },
                    { "Hello $recipientName$, you have received a new message in Altinn from $sendersName$. {textToken} Log in to Altinn inbox to see this message.",
                    "You have received a message in Altinn {textToken}", "en", 1, "Hello $recipientName$, this is a reminder that you have received a new message in Altinn from $sendersName$. {textToken}Log in to Altinn inbox to see this message.",
                    "Reminder - you have received a message in Altinn {textToken}", "Hello $recipientName$, this is a reminder that you have received a new message in Altinn from $sendersName$. {textToken}Log in to Altinn inbox to see this message.",
                     "Hello $recipientName$, you have received a new message in Altinn from $sendersName$. {textToken}Log in to Altinn inbox to see this message.", 1 },

                     //nynorsk
                    { "Hei $recipientName$, du har motteke ei ny melding i Altinn frå $sendersName$. {textToken} Logg deg inn i Altinn inboks for å sjå denne meldinga.",
                    "Du har motteke ei melding i Altinn {textToken}", "nn", 0, "Hei $recipientName$, dette er ei påminning om at du har motteke ei ny melding i Altinn frå $sendersName$. {textToken}Logg deg inn i Altinn inboks for å sjå denne meldinga.",
                    "Påminning - du har motteke ei melding i Altinn {textToken}", "Hei $recipientName$, dette er ei påminning om at du har motteke ei ny melding i Altinn frå $sendersName$. {textToken} Logg deg inn i Altinn inboks for å sjå denne meldinga.",
                     "Hei $recipientName$, du har motteke ei ny melding i Altinn frå $sendersName$. {textToken}Logg deg inn i Altinn inboks for å sjå denne meldinga.", 1 },
                    { "Hei $recipientName$, du har motteke ei ny melding i Altinn frå $sendersName$. {textToken} Logg deg inn i Altinn inboks for å sjå denne meldinga.",
                    "Du har motteke ei melding i Altinn {textToken}", "nn", 1, "Hei $recipientName$, dette er ei påminning om at du har motteke ei ny melding i Altinn frå $sendersName$. {textToken}Logg deg inn i Altinn inboks for å sjå denne meldinga.",
                    "Påminning - du har motteke ei melding i Altinn {textToken}", "Hei $recipientName$, dette er ei påminning om at du har motteke ei ny melding i Altinn frå $sendersName$. {textToken} Logg deg inn i Altinn inboks for å sjå denne meldinga.",
                     "Hei $recipientName$, du har motteke ei ny melding i Altinn frå $sendersName$. {textToken}Logg deg inn i Altinn inboks for å sjå denne meldinga.", 1 }
           });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationTemplates",
                schema: "correspondence");
        }
    }
}
