using System;
using System.Collections.Generic;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace DAL.Data;

public partial class DBContext : DbContext
{
    public DBContext(DbContextOptions<DBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentChapter> DocumentChapters { get; set; }

    public virtual DbSet<DocumentChunk> DocumentChunks { get; set; }

    public virtual DbSet<DocumentFile> DocumentFiles { get; set; }

    public virtual DbSet<UploadJob> UploadJobs { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<ChatSession> ChatSessions { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("pg_trgm")
            .HasPostgresExtension("uuid-ossp")
            .HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("documents_pkey");

            entity.ToTable("documents");

            entity.HasIndex(e => e.OwnerUserId, "idx_documents_owner_user_id");

            entity.HasIndex(e => e.School, "idx_documents_school");

            entity.HasIndex(e => e.Status, "idx_documents_status");

            entity.HasIndex(e => e.Subject, "idx_documents_subject");

            entity.HasIndex(e => e.Visibility, "idx_documents_visibility");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Department)
                .HasMaxLength(200)
                .HasColumnName("department");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Language)
                .HasMaxLength(20)
                .HasDefaultValueSql("'vi'::character varying")
                .HasColumnName("language");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.PageCount).HasColumnName("page_count");
            entity.Property(e => e.School)
                .HasMaxLength(200)
                .HasColumnName("school");
            entity.Property(e => e.SearchText).HasColumnName("search_text");
            entity.Property(e => e.Slug)
                .HasMaxLength(255)
                .HasColumnName("slug");
            entity.Property(e => e.SourceType)
                .HasMaxLength(30)
                .HasDefaultValueSql("'upload'::character varying")
                .HasColumnName("source_type");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Subject)
                .HasMaxLength(200)
                .HasColumnName("subject");
            entity.Property(e => e.TotalChunks)
                .HasDefaultValue(0)
                .HasColumnName("total_chunks");
            entity.Property(e => e.TotalChapters)
                .HasDefaultValue(0)
                .HasColumnName("total_chapters");
            entity.Property(e => e.Title)
                .HasMaxLength(500)
                .HasColumnName("title");
            entity.Property(e => e.Title)
                .HasMaxLength(500)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Visibility)
                .HasMaxLength(50)
                .HasDefaultValueSql("'private'::character varying")
                .HasColumnName("visibility");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.Documents)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("documents_owner_user_id_fkey");

            entity.HasMany(d => d.Tags).WithMany(p => p.Documents)
                .UsingEntity<Dictionary<string, object>>(
                    "DocumentTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("document_tags_tag_id_fkey"),
                    l => l.HasOne<Document>().WithMany()
                        .HasForeignKey("DocumentId")
                        .HasConstraintName("document_tags_document_id_fkey"),
                    j =>
                    {
                        j.HasKey("DocumentId", "TagId").HasName("document_tags_pkey");
                        j.ToTable("document_tags");
                        j.HasIndex(new[] { "TagId" }, "idx_document_tags_tag_id");
                        j.IndexerProperty<Guid>("DocumentId").HasColumnName("document_id");
                        j.IndexerProperty<Guid>("TagId").HasColumnName("tag_id");
                    });
        });

        modelBuilder.Entity<DocumentChapter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("document_chapters_pkey");

            entity.ToTable("document_chapters");

            entity.HasIndex(e => e.DocumentId, "idx_document_chapters_document_id");

            entity.HasIndex(e => e.ParentChapterId, "idx_document_chapters_parent_id");

            entity.HasIndex(e => new { e.DocumentId, e.ChapterOrder }, "idx_document_chapters_order").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score");
            entity.Property(e => e.EndPage).HasColumnName("end_page");
            entity.Property(e => e.StartPage).HasColumnName("start_page");
            entity.Property(e => e.IsAiGenerated).HasColumnName("is_ai_generated");
            entity.Property(e => e.ChapterOrder).HasColumnName("chapter_order");
            entity.Property(e => e.StartChunkIndex).HasColumnName("start_chunk_index");
            entity.Property(e => e.EndChunkIndex).HasColumnName("end_chunk_index");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ParentChapterId).HasColumnName("parent_chapter_id");
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.Title)
                .HasMaxLength(400)
                .HasColumnName("title");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentChapters)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("document_chapters_document_id_fkey");

            entity.HasOne(d => d.ParentChapter).WithMany(p => p.InverseParentChapter)
                .HasForeignKey(d => d.ParentChapterId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("document_chapters_parent_chapter_id_fkey");
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("document_chunks_pkey");

            entity.ToTable("document_chunks");

            entity.HasIndex(e => e.ChapterId, "idx_document_chunks_chapter_id");

            entity.HasIndex(e => e.DocumentId, "idx_document_chunks_document_id");

            entity.HasIndex(e => e.Metadata, "idx_document_chunks_metadata_gin").HasMethod("gin");

            entity.HasIndex(e => e.PageNumber, "idx_document_chunks_page_number");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.ChunkHash)
                .HasMaxLength(64)
                .HasColumnName("chunk_hash");
            entity.Property(e => e.ChunkOrder).HasColumnName("chunk_order");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ContentTokens).HasColumnName("content_tokens");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.Embedding)
                .HasColumnType("vector(1536)")
                .HasColumnName("embedding");
            entity.Property(e => e.Metadata)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("metadata");
            entity.Property(e => e.PageNumber).HasColumnName("page_number");

            entity.HasOne(d => d.Chapter).WithMany(p => p.DocumentChunks)
                .HasForeignKey(d => d.ChapterId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("document_chunks_chapter_id_fkey");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentChunks)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("document_chunks_document_id_fkey");
        });

        modelBuilder.Entity<DocumentFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("document_files_pkey");

            entity.ToTable("document_files");

            entity.HasIndex(e => e.DocumentId, "idx_document_files_document_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.ChecksumSha256)
                .HasMaxLength(64)
                .HasColumnName("checksum_sha256");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ExtractedText).HasColumnName("extracted_text");
            entity.Property(e => e.FileUrl).HasColumnName("file_url");
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(e => e.MimeType)
                .HasMaxLength(100)
                .HasColumnName("mime_type");
            entity.Property(e => e.OriginalFilename)
                .HasMaxLength(255)
                .HasColumnName("original_filename");
            entity.Property(e => e.PageCount).HasColumnName("page_count");
            entity.Property(e => e.ExtractionStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("extraction_status");
            entity.Property(e => e.StoragePath).HasColumnName("storage_path");
            entity.Property(e => e.S3Bucket)
                .HasMaxLength(128)
                .HasColumnName("s3_bucket");
            entity.Property(e => e.S3Key)
                .HasMaxLength(512)
                .HasColumnName("s3_key");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentFiles)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("document_files_document_id_fkey");
        });

        modelBuilder.Entity<UploadJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("upload_jobs_pkey");
            entity.ToTable("upload_jobs");
            entity.HasIndex(e => e.OwnerUserId, "idx_upload_jobs_owner_user_id");
            entity.HasIndex(e => e.DocumentId, "idx_upload_jobs_document_id");
            entity.HasIndex(e => e.Status, "idx_upload_jobs_status");
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.FileName).HasMaxLength(255).HasColumnName("file_name");
            entity.Property(e => e.StoragePath).HasColumnName("storage_path");
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'pending'::character varying").HasColumnName("status");
            entity.Property(e => e.ProgressPercent).HasDefaultValue(0).HasColumnName("progress_percent");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.IsNotified).HasDefaultValue(false).HasColumnName("is_notified");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.HasOne(d => d.OwnerUser).WithMany(p => p.UploadJobs).HasForeignKey(d => d.OwnerUserId).HasConstraintName("upload_jobs_owner_user_id_fkey");
            entity.HasOne(d => d.Document).WithMany(p => p.UploadJobs).HasForeignKey(d => d.DocumentId).HasConstraintName("upload_jobs_document_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Name, "roles_role_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");

            entity.ToTable("tags");

            entity.HasIndex(e => e.Name, "tags_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Slug)
                .HasMaxLength(120)
                .HasColumnName("slug");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.RoleId, "idx_users_role_id");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("users_role_id_fkey");
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_sessions_pkey");

            entity.ToTable("chat_sessions");

            entity.HasIndex(e => e.UserId, "idx_chat_sessions_user_id");

            entity.HasIndex(e => e.CreatedAt, "idx_chat_sessions_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Title)
                .HasMaxLength(500)
                .HasColumnName("title");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.ChatSessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("chat_sessions_user_id_fkey");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_messages_pkey");

            entity.ToTable("chat_messages");

            entity.HasIndex(e => e.SessionId, "idx_chat_messages_session_id");

            entity.HasIndex(e => e.CreatedAt, "idx_chat_messages_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasColumnName("role");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Session).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("chat_messages_session_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
