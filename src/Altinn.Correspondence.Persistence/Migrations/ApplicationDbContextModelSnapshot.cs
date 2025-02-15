﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("correspondence")
                .HasAnnotation("ProductVersion", "9.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "hstore");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.AttachmentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<long>("AttachmentSize")
                        .HasColumnType("bigint");

                    b.Property<string>("Checksum")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("Created")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("DataLocationType")
                        .HasColumnType("integer");

                    b.Property<string>("DataLocationUrl")
                        .HasColumnType("text");

                    b.Property<string>("FileName")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("ResourceId")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("Sender")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("SendersReference")
                        .IsRequired()
                        .HasMaxLength(4096)
                        .HasColumnType("character varying(4096)");

                    b.HasKey("Id");

                    b.ToTable("Attachments", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.AttachmentStatusEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AttachmentId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("PartyUuid")
                        .HasColumnType("uuid");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("StatusChanged")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("StatusText")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("AttachmentId");

                    b.ToTable("AttachmentStatuses", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceAttachmentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AttachmentId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceContentId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("Created")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("ExpirationTime")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AttachmentId");

                    b.HasIndex("CorrespondenceContentId");

                    b.ToTable("CorrespondenceAttachments", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceContentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<string>("Language")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("MessageBody")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("MessageSummary")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("MessageTitle")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId")
                        .IsUnique();

                    b.ToTable("CorrespondenceContents", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("AllowSystemDeleteAfter")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("Altinn2CorrespondenceId")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("Created")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset?>("DueDateTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool?>("IgnoreReservation")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsConfirmationNeeded")
                        .HasColumnType("boolean");

                    b.Property<string>("MessageSender")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<Dictionary<string, string>>("PropertyList")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("hstore");

                    b.Property<DateTimeOffset?>("Published")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Recipient")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("RequestedPublishTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ResourceId")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("Sender")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("SendersReference")
                        .IsRequired()
                        .HasMaxLength(4096)
                        .HasColumnType("character varying(4096)");

                    b.HasKey("Id");

                    b.HasIndex("Created");

                    b.HasIndex("Recipient");

                    b.HasIndex("ResourceId");

                    b.HasIndex("Sender");

                    b.ToTable("Correspondences", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceNotificationEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int?>("Altinn2NotificationId")
                        .HasColumnType("integer");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("Created")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsReminder")
                        .HasColumnType("boolean");

                    b.Property<string>("NotificationAddress")
                        .HasColumnType("text");

                    b.Property<int>("NotificationChannel")
                        .HasColumnType("integer");

                    b.Property<Guid?>("NotificationOrderId")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("NotificationSent")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("NotificationTemplate")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("RequestedSendTime")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId");

                    b.ToTable("CorrespondenceNotifications", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceReplyOptionEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<string>("LinkText")
                        .HasColumnType("text");

                    b.Property<string>("LinkURL")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId");

                    b.ToTable("CorrespondenceReplyOptions", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceStatusEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("PartyUuid")
                        .HasColumnType("uuid");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("StatusChanged")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("StatusText")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId");

                    b.HasIndex("Status");

                    b.ToTable("CorrespondenceStatuses", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.ExternalReferenceEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<int>("ReferenceType")
                        .HasColumnType("integer");

                    b.Property<string>("ReferenceValue")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId");

                    b.ToTable("ExternalReferences", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.LegacyPartyEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("PartyId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("LegacyParties", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.NotificationTemplateEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("EmailBody")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("EmailSubject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Language")
                        .HasColumnType("text");

                    b.Property<int?>("RecipientType")
                        .HasColumnType("integer");

                    b.Property<string>("ReminderEmailBody")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ReminderEmailSubject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ReminderSmsBody")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("SmsBody")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Template")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("NotificationTemplates", "correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.AttachmentStatusEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.AttachmentEntity", "Attachment")
                        .WithMany("Statuses")
                        .HasForeignKey("AttachmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attachment");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceAttachmentEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.AttachmentEntity", "Attachment")
                        .WithMany("CorrespondenceAttachments")
                        .HasForeignKey("AttachmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.CorrespondenceContentEntity", "CorrespondenceContent")
                        .WithMany("Attachments")
                        .HasForeignKey("CorrespondenceContentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attachment");

                    b.Navigation("CorrespondenceContent");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceContentEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.CorrespondenceEntity", "Correspondence")
                        .WithOne("Content")
                        .HasForeignKey("Altinn.Correspondence.Core.Models.Entities.CorrespondenceContentEntity", "CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceNotificationEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.CorrespondenceEntity", "Correspondence")
                        .WithMany("Notifications")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceReplyOptionEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.CorrespondenceEntity", "Correspondence")
                        .WithMany("ReplyOptions")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceStatusEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.CorrespondenceEntity", "Correspondence")
                        .WithMany("Statuses")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.ExternalReferenceEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.Entities.CorrespondenceEntity", "Correspondence")
                        .WithMany("ExternalReferences")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.AttachmentEntity", b =>
                {
                    b.Navigation("CorrespondenceAttachments");

                    b.Navigation("Statuses");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceContentEntity", b =>
                {
                    b.Navigation("Attachments");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.Entities.CorrespondenceEntity", b =>
                {
                    b.Navigation("Content");

                    b.Navigation("ExternalReferences");

                    b.Navigation("Notifications");

                    b.Navigation("ReplyOptions");

                    b.Navigation("Statuses");
                });
#pragma warning restore 612, 618
        }
    }
}
