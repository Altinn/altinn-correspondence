﻿// <auto-generated />
using System;
using System.Collections.Generic;
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
                .HasAnnotation("ProductVersion", "8.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "hstore");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.AttachmentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Checksum")
                        .HasColumnType("text");

                    b.Property<int>("DataLocationType")
                        .HasColumnType("integer");

                    b.Property<string>("DataLocationUrl")
                        .HasColumnType("text");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("ExpirationTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("FileName")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<int>("IntendedPresentation")
                        .HasColumnType("integer");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean");

                    b.Property<string>("RestrictionName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("SendersReference")
                        .IsRequired()
                        .HasMaxLength(4096)
                        .HasColumnType("character varying(4096)");

                    b.HasKey("Id");

                    b.ToTable("Attachments");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.AttachmentStatusEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AttachmentId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("CorrespondenceAttachmentEntityId")
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

                    b.HasIndex("CorrespondenceAttachmentEntityId");

                    b.ToTable("AttachmentStatuses");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceAttachmentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AttachmentId")
                        .HasColumnType("uuid");

                    b.Property<string>("Checksum")
                        .HasColumnType("text");

                    b.Property<Guid?>("CorrespondenceContentEntityId")
                        .HasColumnType("uuid");

                    b.Property<int>("DataLocationType")
                        .HasColumnType("integer");

                    b.Property<string>("DataLocationUrl")
                        .HasColumnType("text");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("ExpirationTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("IntendedPresentation")
                        .HasColumnType("integer");

                    b.Property<bool>("IsEncrypted")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("RestrictionName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("SendersReference")
                        .IsRequired()
                        .HasMaxLength(4096)
                        .HasColumnType("character varying(4096)");

                    b.HasKey("Id");

                    b.HasIndex("AttachmentId");

                    b.HasIndex("CorrespondenceContentEntityId");

                    b.ToTable("CorrespondenceAttachmentEntity");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceContentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<int>("Language")
                        .HasColumnType("integer");

                    b.Property<string>("MessageSummary")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("MessageTitle")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId")
                        .IsUnique();

                    b.ToTable("CorrespondenceContents");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("AllowSystemDeleteAfter")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("DueDateTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool?>("IsReservable")
                        .HasColumnType("boolean");

                    b.Property<Dictionary<string, string>>("PropertyList")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("hstore");

                    b.Property<string>("Recipient")
                        .IsRequired()
                        .HasColumnType("text");

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

                    b.Property<DateTimeOffset>("VisibleFrom")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.ToTable("Correspondences");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceNotificationEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<string>("CustomTextToken")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<string>("NotificationTemplate")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("RequestedSendTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("SendersReference")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId");

                    b.ToTable("CorrespondenceNotifications");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceReplyOptionEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
                        .HasColumnType("uuid");

                    b.Property<string>("LinkText")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("LinkURL")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("CorrespondenceId");

                    b.ToTable("CorrespondenceReplyOptions");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceStatusEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CorrespondenceId")
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

                    b.ToTable("CorrespondenceStatuses");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.ExternalReferenceEntity", b =>
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

                    b.ToTable("ExternalReferences");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.AttachmentStatusEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.AttachmentEntity", "Attachment")
                        .WithMany("Statuses")
                        .HasForeignKey("AttachmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Altinn.Correspondence.Core.Models.CorrespondenceAttachmentEntity", null)
                        .WithMany("Statuses")
                        .HasForeignKey("CorrespondenceAttachmentEntityId");

                    b.Navigation("Attachment");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceAttachmentEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.AttachmentEntity", "Attachment")
                        .WithMany("CorrespondenceAttachments")
                        .HasForeignKey("AttachmentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Altinn.Correspondence.Core.Models.CorrespondenceContentEntity", null)
                        .WithMany("Attachments")
                        .HasForeignKey("CorrespondenceContentEntityId");

                    b.Navigation("Attachment");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceContentEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.CorrespondenceEntity", "Correspondence")
                        .WithOne("Content")
                        .HasForeignKey("Altinn.Correspondence.Core.Models.CorrespondenceContentEntity", "CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceNotificationEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.CorrespondenceEntity", "Correspondence")
                        .WithMany("Notifications")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceReplyOptionEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.CorrespondenceEntity", "Correspondence")
                        .WithMany("ReplyOptions")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceStatusEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.CorrespondenceEntity", "Correspondence")
                        .WithMany("Statuses")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.ExternalReferenceEntity", b =>
                {
                    b.HasOne("Altinn.Correspondence.Core.Models.CorrespondenceEntity", "Correspondence")
                        .WithMany("ExternalReferences")
                        .HasForeignKey("CorrespondenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Correspondence");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.AttachmentEntity", b =>
                {
                    b.Navigation("CorrespondenceAttachments");

                    b.Navigation("Statuses");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceAttachmentEntity", b =>
                {
                    b.Navigation("Statuses");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceContentEntity", b =>
                {
                    b.Navigation("Attachments");
                });

            modelBuilder.Entity("Altinn.Correspondence.Core.Models.CorrespondenceEntity", b =>
                {
                    b.Navigation("Content")
                        .IsRequired();

                    b.Navigation("ExternalReferences");

                    b.Navigation("Notifications");

                    b.Navigation("ReplyOptions");

                    b.Navigation("Statuses");
                });
#pragma warning restore 612, 618
        }
    }
}
