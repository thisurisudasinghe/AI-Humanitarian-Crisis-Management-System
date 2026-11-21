-- ============================================================================
-- 03_ai.sql - Dev 3 AI decision support and governance schema (M3/M4)
-- ============================================================================
-- Creates the AI tables owned by Dev 3:
--   ai.recommendations, ai.decision_records, ai.announcements
--
-- Run after 00_common.sql and before 98_foreign_keys.sql. The script is
-- idempotent: missing schemas, tables, indexes and foreign keys are created;
-- existing objects are left untouched. It never drops data.
--
-- The EF model (CmsDbContext + Configurations/AI) maps the same tables and
-- columns through NamingConventions.ApplySnakeCase.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'ai') IS NULL
    EXEC(N'CREATE SCHEMA [ai] AUTHORIZATION [dbo];');
GO

-- ----------------------------------------------------------------------------
-- ai.recommendations
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[ai].[recommendations]', N'U') IS NULL
BEGIN
    CREATE TABLE [ai].[recommendations] (
        [id] uniqueidentifier NOT NULL,
        [type] nvarchar(100) NOT NULL,
        [target_id] uniqueidentifier NOT NULL,
        [payload_json] nvarchar(max) NOT NULL,
        [confidence] decimal(5, 4) NOT NULL,
        [rationale] nvarchar(max) NOT NULL,
        [source] nvarchar(100) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_recommendations] PRIMARY KEY ([id])
    );
END;
GO

-- Existing databases were created before the required Status column existed.
-- Add it without replacing the table or its data.
IF COL_LENGTH(N'[ai].[recommendations]', N'status') IS NULL
BEGIN
    ALTER TABLE [ai].[recommendations]
        ADD [status] nvarchar(30) NOT NULL
            CONSTRAINT [DF_recommendations_status] DEFAULT (N'Pending');
END;
GO

-- ----------------------------------------------------------------------------
-- ai.decision_records
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[ai].[decision_records]', N'U') IS NULL
BEGIN
    CREATE TABLE [ai].[decision_records] (
        [id] uniqueidentifier NOT NULL,
        [recommendation_id] uniqueidentifier NOT NULL,
        [outcome] nvarchar(20) NOT NULL,
        [final_payload_json] nvarchar(max) NULL,
        [reason] nvarchar(2000) NOT NULL,
        [decided_by_user_id] uniqueidentifier NOT NULL,
        [decided_at] datetime2 NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_decision_records] PRIMARY KEY ([id]),
        CONSTRAINT [FK_decision_records_recommendations]
            FOREIGN KEY ([recommendation_id])
            REFERENCES [ai].[recommendations] ([id]) ON DELETE NO ACTION
    );
END;
GO

-- Human decisions are append-only. The service also prevents a second decision,
-- but this trigger makes accidental UPDATE/DELETE code impossible at the DB edge.
CREATE OR ALTER TRIGGER [ai].[TR_decision_records_immutable]
    ON [ai].[decision_records]
    AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    RAISERROR(N'Decision records are immutable; record a new decision instead.', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO

-- ----------------------------------------------------------------------------
-- ai.announcements
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[ai].[announcements]', N'U') IS NULL
BEGIN
    CREATE TABLE [ai].[announcements] (
        [id] uniqueidentifier NOT NULL,
        [title] nvarchar(200) NOT NULL,
        [message] nvarchar(4000) NOT NULL,
        [audience_json] nvarchar(max) NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [channel] nvarchar(50) NOT NULL,
        [requested_by_user_id] uniqueidentifier NOT NULL,
        [approved_by_user_id] uniqueidentifier NULL,
        [approved_at] datetime2 NULL,
        [rejection_reason] nvarchar(2000) NULL,
        [dispatched_at] datetime2 NULL,
        [recipient_count] int NOT NULL DEFAULT 0,
        [idempotency_key] nvarchar(200) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_announcements] PRIMARY KEY ([id])
    );
END;
GO

-- Existing announcements need the language used for multilingual drafts.
IF COL_LENGTH(N'[ai].[announcements]', N'language') IS NULL
BEGIN
    ALTER TABLE [ai].[announcements]
        ADD [language] nvarchar(10) NOT NULL
            CONSTRAINT [DF_announcements_language] DEFAULT (N'en');
END;
GO

-- ----------------------------------------------------------------------------
-- Indexes
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_recommendations_target_type'
      AND [object_id] = OBJECT_ID(N'[ai].[recommendations]'))
    CREATE INDEX [IX_recommendations_target_type]
        ON [ai].[recommendations] ([target_id], [type]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_decision_records_recommendation_id'
      AND [object_id] = OBJECT_ID(N'[ai].[decision_records]'))
    CREATE INDEX [IX_decision_records_recommendation_id]
        ON [ai].[decision_records] ([recommendation_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_announcements_idempotency_key'
      AND [object_id] = OBJECT_ID(N'[ai].[announcements]'))
    CREATE UNIQUE INDEX [UX_announcements_idempotency_key]
        ON [ai].[announcements] ([idempotency_key]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_announcements_status'
      AND [object_id] = OBJECT_ID(N'[ai].[announcements]'))
    CREATE INDEX [IX_announcements_status]
        ON [ai].[announcements] ([status]);
GO
