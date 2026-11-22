-- Owner: Developer 2 (M2 Crisis & Case Management)
-- Run order: after 00_common.sql (Member 1), before 98_foreign_keys.sql.
-- Schema: cases. D-01: hand-written plain SQL; no EF migration.
-- Safe to re-run where practical. Cross-module FKs are intentionally omitted.
-- Handoff for Member 1's 98_foreign_keys.sql:
--   cases.requests.requester_user_id     -> identity.users.id
--   cases.requests.assigned_to_user_id   -> identity.users.id
--   cases.households.user_id             -> identity.users.id
--   cases.partner_shares.organisation_id -> common.organisations.id

IF SCHEMA_ID(N'cases') IS NULL
BEGIN
    EXEC('CREATE SCHEMA [cases]');
END
GO

IF OBJECT_ID(N'cases.crises', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[crises] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [name] NVARCHAR(200) NOT NULL,
        [type] NVARCHAR(30) NOT NULL,
        [description] NVARCHAR(2000) NULL,
        [status] NVARCHAR(30) NOT NULL,
        [started_at] DATETIME2(7) NOT NULL,
        [ended_at] DATETIME2(7) NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_crises] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.areas', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[areas] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [crisis_id] UNIQUEIDENTIFIER NOT NULL,
        [name] NVARCHAR(200) NOT NULL,
        [description] NVARCHAR(2000) NULL,
        [lat] DECIMAL(18,7) NOT NULL,
        [lng] DECIMAL(18,7) NOT NULL,
        [radius_km] DECIMAL(10,2) NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_areas] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.households', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[households] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [user_id] UNIQUEIDENTIFIER NOT NULL,
        [size] INT NOT NULL,
        [accessibility_needs] NVARCHAR(2000) NULL,
        [contact_phone] NVARCHAR(500) NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_households] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.requests', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[requests] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [requester_user_id] UNIQUEIDENTIFIER NOT NULL,
        [crisis_id] UNIQUEIDENTIFIER NULL,
        [area_id] UNIQUEIDENTIFIER NULL,
        [category] NVARCHAR(30) NOT NULL,
        [urgency] NVARCHAR(30) NOT NULL,
        [description] NVARCHAR(2000) NOT NULL,
        [lat] DECIMAL(18,7) NOT NULL,
        [lng] DECIMAL(18,7) NOT NULL,
        [address] NVARCHAR(500) NULL,
        [household_size] INT NOT NULL,
        [accessibility_needs] NVARCHAR(2000) NULL,
        [status] NVARCHAR(30) NOT NULL,
        [assigned_to_user_id] UNIQUEIDENTIFIER NULL,
        [merged_into_request_id] UNIQUEIDENTIFIER NULL,
        [submitted_at] DATETIME2(7) NOT NULL,
        [verified_at] DATETIME2(7) NULL,
        [triaged_at] DATETIME2(7) NULL,
        [closed_at] DATETIME2(7) NULL,
        [row_version] ROWVERSION NOT NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_requests] PRIMARY KEY ([id])
    );
END
GO

IF COL_LENGTH(N'cases.requests', N'address') IS NULL
BEGIN
    ALTER TABLE [cases].[requests] ADD [address] NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID(N'cases.request_status_history', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[request_status_history] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [request_id] UNIQUEIDENTIFIER NOT NULL,
        [from_status] NVARCHAR(30) NOT NULL,
        [to_status] NVARCHAR(30) NOT NULL,
        [changed_by_user_id] UNIQUEIDENTIFIER NULL,
        [reason] NVARCHAR(1000) NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_request_status_history] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.request_links', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[request_links] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [request_id] UNIQUEIDENTIFIER NOT NULL,
        [linked_request_id] UNIQUEIDENTIFIER NOT NULL,
        [link_type] NVARCHAR(30) NOT NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_request_links] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.attachments', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[attachments] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [request_id] UNIQUEIDENTIFIER NOT NULL,
        [stored_file_name] NVARCHAR(255) NOT NULL,
        [original_file_name] NVARCHAR(255) NOT NULL,
        [content_type] NVARCHAR(100) NOT NULL,
        [size_bytes] BIGINT NOT NULL,
        [uploaded_by_user_id] UNIQUEIDENTIFIER NOT NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_attachments] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.alerts', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[alerts] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [crisis_id] UNIQUEIDENTIFIER NULL,
        [title] NVARCHAR(200) NOT NULL,
        [body] NVARCHAR(2000) NOT NULL,
        [status] NVARCHAR(30) NOT NULL,
        [published_at] DATETIME2(7) NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_alerts] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.service_centres', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[service_centres] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [crisis_id] UNIQUEIDENTIFIER NULL,
        [name] NVARCHAR(200) NOT NULL,
        [address] NVARCHAR(500) NOT NULL,
        [lat] DECIMAL(18,7) NOT NULL,
        [lng] DECIMAL(18,7) NOT NULL,
        [services] NVARCHAR(1000) NOT NULL,
        [opening_hours] NVARCHAR(500) NULL,
        [is_active] BIT NOT NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_service_centres] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.feedback', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[feedback] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [request_id] UNIQUEIDENTIFIER NOT NULL,
        [submitted_by_user_id] UNIQUEIDENTIFIER NOT NULL,
        [comment] NVARCHAR(2000) NOT NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_feedback] PRIMARY KEY ([id])
    );
END
GO

IF OBJECT_ID(N'cases.partner_shares', N'U') IS NULL
BEGIN
    CREATE TABLE [cases].[partner_shares] (
        [id] UNIQUEIDENTIFIER NOT NULL,
        [request_id] UNIQUEIDENTIFIER NOT NULL,
        [organisation_id] UNIQUEIDENTIFIER NOT NULL,
        [shared_by_user_id] UNIQUEIDENTIFIER NOT NULL,
        [shared_at] DATETIME2(7) NOT NULL,
        [revoked_at] DATETIME2(7) NULL,
        [created_at] DATETIME2(7) NOT NULL,
        [updated_at] DATETIME2(7) NULL,
        CONSTRAINT [PK_partner_shares] PRIMARY KEY ([id])
    );
END
GO

-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_crises_status' AND object_id = OBJECT_ID(N'cases.crises'))
    CREATE INDEX [IX_crises_status] ON [cases].[crises]([status]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_areas_crisis_id' AND object_id = OBJECT_ID(N'cases.areas'))
    CREATE INDEX [IX_areas_crisis_id] ON [cases].[areas]([crisis_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_households_user_id' AND object_id = OBJECT_ID(N'cases.households'))
    CREATE UNIQUE INDEX [IX_households_user_id] ON [cases].[households]([user_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_requests_status_created_at' AND object_id = OBJECT_ID(N'cases.requests'))
    CREATE INDEX [IX_requests_status_created_at] ON [cases].[requests]([status], [created_at]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_requests_requester_user_id' AND object_id = OBJECT_ID(N'cases.requests'))
    CREATE INDEX [IX_requests_requester_user_id] ON [cases].[requests]([requester_user_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_requests_area_id' AND object_id = OBJECT_ID(N'cases.requests'))
    CREATE INDEX [IX_requests_area_id] ON [cases].[requests]([area_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_requests_assigned_to_user_id' AND object_id = OBJECT_ID(N'cases.requests'))
    CREATE INDEX [IX_requests_assigned_to_user_id] ON [cases].[requests]([assigned_to_user_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_requests_crisis_id' AND object_id = OBJECT_ID(N'cases.requests'))
    CREATE INDEX [IX_requests_crisis_id] ON [cases].[requests]([crisis_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_request_status_history_request_id_created_at' AND object_id = OBJECT_ID(N'cases.request_status_history'))
    CREATE INDEX [IX_request_status_history_request_id_created_at] ON [cases].[request_status_history]([request_id], [created_at]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_request_links_request_id' AND object_id = OBJECT_ID(N'cases.request_links'))
    CREATE INDEX [IX_request_links_request_id] ON [cases].[request_links]([request_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_request_links_linked_request_id' AND object_id = OBJECT_ID(N'cases.request_links'))
    CREATE INDEX [IX_request_links_linked_request_id] ON [cases].[request_links]([linked_request_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_attachments_request_id' AND object_id = OBJECT_ID(N'cases.attachments'))
    CREATE INDEX [IX_attachments_request_id] ON [cases].[attachments]([request_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_alerts_status_published_at' AND object_id = OBJECT_ID(N'cases.alerts'))
    CREATE INDEX [IX_alerts_status_published_at] ON [cases].[alerts]([status], [published_at]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_service_centres_is_active' AND object_id = OBJECT_ID(N'cases.service_centres'))
    CREATE INDEX [IX_service_centres_is_active] ON [cases].[service_centres]([is_active]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_feedback_request_id' AND object_id = OBJECT_ID(N'cases.feedback'))
    CREATE INDEX [IX_feedback_request_id] ON [cases].[feedback]([request_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_partner_shares_request_id' AND object_id = OBJECT_ID(N'cases.partner_shares'))
    CREATE INDEX [IX_partner_shares_request_id] ON [cases].[partner_shares]([request_id]);
GO

-- Intra-schema foreign keys only. Cross-module FKs belong in 98_foreign_keys.sql.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_areas_crises')
    ALTER TABLE [cases].[areas] ADD CONSTRAINT [FK_areas_crises] FOREIGN KEY ([crisis_id]) REFERENCES [cases].[crises]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_requests_crises')
    ALTER TABLE [cases].[requests] ADD CONSTRAINT [FK_requests_crises] FOREIGN KEY ([crisis_id]) REFERENCES [cases].[crises]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_requests_areas')
    ALTER TABLE [cases].[requests] ADD CONSTRAINT [FK_requests_areas] FOREIGN KEY ([area_id]) REFERENCES [cases].[areas]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_requests_merged_target')
    ALTER TABLE [cases].[requests] ADD CONSTRAINT [FK_requests_merged_target] FOREIGN KEY ([merged_into_request_id]) REFERENCES [cases].[requests]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_request_status_history_requests')
    ALTER TABLE [cases].[request_status_history] ADD CONSTRAINT [FK_request_status_history_requests] FOREIGN KEY ([request_id]) REFERENCES [cases].[requests]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_request_links_source')
    ALTER TABLE [cases].[request_links] ADD CONSTRAINT [FK_request_links_source] FOREIGN KEY ([request_id]) REFERENCES [cases].[requests]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_request_links_target')
    ALTER TABLE [cases].[request_links] ADD CONSTRAINT [FK_request_links_target] FOREIGN KEY ([linked_request_id]) REFERENCES [cases].[requests]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_attachments_requests')
    ALTER TABLE [cases].[attachments] ADD CONSTRAINT [FK_attachments_requests] FOREIGN KEY ([request_id]) REFERENCES [cases].[requests]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_alerts_crises')
    ALTER TABLE [cases].[alerts] ADD CONSTRAINT [FK_alerts_crises] FOREIGN KEY ([crisis_id]) REFERENCES [cases].[crises]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_service_centres_crises')
    ALTER TABLE [cases].[service_centres] ADD CONSTRAINT [FK_service_centres_crises] FOREIGN KEY ([crisis_id]) REFERENCES [cases].[crises]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_feedback_requests')
    ALTER TABLE [cases].[feedback] ADD CONSTRAINT [FK_feedback_requests] FOREIGN KEY ([request_id]) REFERENCES [cases].[requests]([id]) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_partner_shares_requests')
    ALTER TABLE [cases].[partner_shares] ADD CONSTRAINT [FK_partner_shares_requests] FOREIGN KEY ([request_id]) REFERENCES [cases].[requests]([id]) ON DELETE NO ACTION;
GO
