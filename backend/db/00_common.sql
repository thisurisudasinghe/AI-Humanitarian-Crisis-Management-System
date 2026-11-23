-- ============================================================================
-- 00_common.sql - Dev 1 foundation schema (identity + common)
-- ============================================================================
-- Creates the shared tables owned by Dev 1 for the V4 snake_case model:
--   common.organisations, common.system_settings, common.audit_events,
--   common.outbox_messages
--   identity.roles, identity.users, identity.refresh_tokens, identity.user_roles
--
-- Canonical run order:
--   00_common -> 01_cases ... 08_don -> 98_foreign_keys -> 99_seed_users
--   -> 90_seed_scenario -> 91_seed_ops
--
-- This script is idempotent. It creates only missing schemas, tables and
-- indexes; existing objects are left untouched. It never drops data.
--
-- QUOTED_IDENTIFIER ON is required for the filtered unique index on
-- identity.users.email_hash. Run with sqlcmd -I as well.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ----------------------------------------------------------------------------
-- Schemas
-- ----------------------------------------------------------------------------
IF SCHEMA_ID(N'common') IS NULL
    EXEC(N'CREATE SCHEMA [common] AUTHORIZATION [dbo];');
GO

IF SCHEMA_ID(N'identity') IS NULL
    EXEC(N'CREATE SCHEMA [identity] AUTHORIZATION [dbo];');
GO

-- ----------------------------------------------------------------------------
-- common.organisations
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[common].[organisations]', N'U') IS NULL
BEGIN
    CREATE TABLE [common].[organisations] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(200) NOT NULL,
        [organisation_type] nvarchar(100) NOT NULL,
        [registration_number] nvarchar(100) NULL,
        [contact_email] nvarchar(500) NULL,
        [contact_phone] nvarchar(20) NULL,
        [address] nvarchar(500) NULL,
        [is_active] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_organisations] PRIMARY KEY ([id])
    );
END;
GO

-- ----------------------------------------------------------------------------
-- common.audit_events (append-only; no updated_at by design)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[common].[audit_events]', N'U') IS NULL
BEGIN
    CREATE TABLE [common].[audit_events] (
        [id] uniqueidentifier NOT NULL,
        [action] nvarchar(200) NOT NULL,
        [entity_type] nvarchar(100) NOT NULL,
        [entity_id] nvarchar(100) NULL,
        [actor_user_id] uniqueidentifier NULL,
        [details] nvarchar(max) NULL,
        [ip_address] nvarchar(45) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_audit_events] PRIMARY KEY ([id])
    );
END;
GO

-- ----------------------------------------------------------------------------
-- common.system_settings
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[common].[system_settings]', N'U') IS NULL
BEGIN
    CREATE TABLE [common].[system_settings] (
        [id] uniqueidentifier NOT NULL,
        [key] nvarchar(200) NOT NULL,
        [value] nvarchar(4000) NOT NULL,
        [description] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_system_settings] PRIMARY KEY ([id])
    );
END;
GO

-- ----------------------------------------------------------------------------
-- common.outbox_messages (transactional outbox, owned by Dev 1)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[common].[outbox_messages]', N'U') IS NULL
BEGIN
    CREATE TABLE [common].[outbox_messages] (
        [id] uniqueidentifier NOT NULL,
        [type] nvarchar(100) NOT NULL,
        [entity_id] uniqueidentifier NOT NULL,
        [payload] nvarchar(max) NOT NULL,
        [idempotency_key] nvarchar(256) NOT NULL,
        [status] nvarchar(50) NOT NULL,
        [retry_count] int NOT NULL,
        [next_retry_at] datetime2 NULL,
        [processed_at] datetime2 NULL,
        [error_message] nvarchar(max) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_outbox_messages] PRIMARY KEY ([id])
    );
END;
GO

-- ----------------------------------------------------------------------------
-- identity.roles
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[identity].[roles]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[roles] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(100) NOT NULL,
        [description] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_roles] PRIMARY KEY ([id])
    );
END;
GO

-- ----------------------------------------------------------------------------
-- identity.users
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[identity].[users]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[users] (
        [id] uniqueidentifier NOT NULL,
        [organisation_id] uniqueidentifier NULL,
        [first_name] nvarchar(100) NOT NULL,
        [last_name] nvarchar(100) NOT NULL,
        [phone_number] nvarchar(20) NULL,
        [email_encrypted] nvarchar(1000) NULL,
        [email_hash] nvarchar(64) NULL,
        [password_hash] nvarchar(500) NOT NULL,
        [is_email_verified] bit NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [last_login_at] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([id]),
        CONSTRAINT [FK_users_organisations] FOREIGN KEY ([organisation_id])
            REFERENCES [common].[organisations] ([id]) ON DELETE NO ACTION
    );
END;
GO

-- ----------------------------------------------------------------------------
-- identity.refresh_tokens
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[identity].[refresh_tokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[refresh_tokens] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [token_hash] nvarchar(64) NOT NULL,
        [expires_at] datetime2 NOT NULL,
        [is_revoked] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_refresh_tokens] PRIMARY KEY ([id]),
        CONSTRAINT [FK_refresh_tokens_users] FOREIGN KEY ([user_id])
            REFERENCES [identity].[users] ([id]) ON DELETE CASCADE
    );
END;
GO

-- ----------------------------------------------------------------------------
-- identity.password_reset_tokens
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[identity].[password_reset_tokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[password_reset_tokens] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [token_hash] nvarchar(64) NOT NULL,
        [expires_at] datetime2 NOT NULL,
        [used_at] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_password_reset_tokens] PRIMARY KEY ([id]),
        CONSTRAINT [FK_password_reset_tokens_users] FOREIGN KEY ([user_id])
            REFERENCES [identity].[users] ([id]) ON DELETE CASCADE
    );
END;
GO

-- ----------------------------------------------------------------------------
-- identity.user_roles
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[identity].[user_roles]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[user_roles] (
        [user_id] uniqueidentifier NOT NULL,
        [role_id] uniqueidentifier NOT NULL,
        [assigned_at] datetime2 NOT NULL,
        CONSTRAINT [PK_user_roles] PRIMARY KEY ([user_id], [role_id]),
        CONSTRAINT [FK_user_roles_roles] FOREIGN KEY ([role_id])
            REFERENCES [identity].[roles] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_user_roles_users] FOREIGN KEY ([user_id])
            REFERENCES [identity].[users] ([id]) ON DELETE CASCADE
    );
END;
GO

-- ----------------------------------------------------------------------------
-- common.notifications (delivery records, owned by Dev 1 / M9)
-- One row per (outbox message, channel). The in-app row is the recipient's
-- inbox entry; the email row records SMTP delivery status.
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[common].[notifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [common].[notifications] (
        [id] uniqueidentifier NOT NULL,
        [recipient_user_id] uniqueidentifier NOT NULL,
        [outbox_message_id] uniqueidentifier NULL,
        [channel] nvarchar(50) NOT NULL,
        [template_key] nvarchar(100) NOT NULL,
        [subject] nvarchar(500) NULL,
        [body] nvarchar(max) NULL,
        [status] nvarchar(50) NOT NULL,
        [sent_at] datetime2 NULL,
        [read_at] datetime2 NULL,
        [request_id] uniqueidentifier NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_notifications] PRIMARY KEY ([id]),
        CONSTRAINT [FK_notifications_users] FOREIGN KEY ([recipient_user_id])
            REFERENCES [identity].[users] ([id]) ON DELETE CASCADE
    );
END;
GO

-- ----------------------------------------------------------------------------
-- Indexes
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_organisations_name'
      AND [object_id] = OBJECT_ID(N'[common].[organisations]'))
    CREATE INDEX [IX_organisations_name]
        ON [common].[organisations] ([name]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_audit_events_actor_user_id'
      AND [object_id] = OBJECT_ID(N'[common].[audit_events]'))
    CREATE INDEX [IX_audit_events_actor_user_id]
        ON [common].[audit_events] ([actor_user_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_audit_events_created_at'
      AND [object_id] = OBJECT_ID(N'[common].[audit_events]'))
    CREATE INDEX [IX_audit_events_created_at]
        ON [common].[audit_events] ([created_at]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_audit_events_entity'
      AND [object_id] = OBJECT_ID(N'[common].[audit_events]'))
    CREATE INDEX [IX_audit_events_entity]
        ON [common].[audit_events] ([entity_type], [entity_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_system_settings_key'
      AND [object_id] = OBJECT_ID(N'[common].[system_settings]'))
    CREATE UNIQUE INDEX [UX_system_settings_key]
        ON [common].[system_settings] ([key]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_outbox_idempotency_key'
      AND [object_id] = OBJECT_ID(N'[common].[outbox_messages]'))
    CREATE UNIQUE INDEX [UX_outbox_idempotency_key]
        ON [common].[outbox_messages] ([idempotency_key]);
GO

-- SQL Server filtered indexes reject OR/IN in the predicate (verified on
-- SQL Server 2025), so the contract's single OR index is implemented as two
-- equivalent filtered indexes. QUOTED_IDENTIFIER ON is required.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_outbox_status_retry_pending'
      AND [object_id] = OBJECT_ID(N'[common].[outbox_messages]'))
    CREATE INDEX [IX_outbox_status_retry_pending]
        ON [common].[outbox_messages] ([status], [next_retry_at])
        WHERE [status] = 'Pending';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_outbox_status_retry_failed'
      AND [object_id] = OBJECT_ID(N'[common].[outbox_messages]'))
    CREATE INDEX [IX_outbox_status_retry_failed]
        ON [common].[outbox_messages] ([status], [next_retry_at])
        WHERE [status] = 'Failed';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_outbox_created_at'
      AND [object_id] = OBJECT_ID(N'[common].[outbox_messages]'))
    CREATE INDEX [IX_outbox_created_at]
        ON [common].[outbox_messages] ([created_at]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_roles_name'
      AND [object_id] = OBJECT_ID(N'[identity].[roles]'))
    CREATE UNIQUE INDEX [UX_roles_name]
        ON [identity].[roles] ([name]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_users_organisation_id'
      AND [object_id] = OBJECT_ID(N'[identity].[users]'))
    CREATE INDEX [IX_users_organisation_id]
        ON [identity].[users] ([organisation_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_users_email_hash'
      AND [object_id] = OBJECT_ID(N'[identity].[users]'))
    CREATE UNIQUE INDEX [UX_users_email_hash]
        ON [identity].[users] ([email_hash])
        WHERE [email_hash] IS NOT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_refresh_tokens_user_id'
      AND [object_id] = OBJECT_ID(N'[identity].[refresh_tokens]'))
    CREATE INDEX [IX_refresh_tokens_user_id]
        ON [identity].[refresh_tokens] ([user_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_refresh_tokens_token_hash'
      AND [object_id] = OBJECT_ID(N'[identity].[refresh_tokens]'))
    CREATE UNIQUE INDEX [UX_refresh_tokens_token_hash]
        ON [identity].[refresh_tokens] ([token_hash]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_password_reset_tokens_user_id'
      AND [object_id] = OBJECT_ID(N'[identity].[password_reset_tokens]'))
    CREATE INDEX [IX_password_reset_tokens_user_id]
        ON [identity].[password_reset_tokens] ([user_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_password_reset_tokens_token_hash'
      AND [object_id] = OBJECT_ID(N'[identity].[password_reset_tokens]'))
    CREATE UNIQUE INDEX [UX_password_reset_tokens_token_hash]
        ON [identity].[password_reset_tokens] ([token_hash]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_user_roles_role_id'
      AND [object_id] = OBJECT_ID(N'[identity].[user_roles]'))
    CREATE INDEX [IX_user_roles_role_id]
        ON [identity].[user_roles] ([role_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_notifications_recipient'
      AND [object_id] = OBJECT_ID(N'[common].[notifications]'))
    CREATE INDEX [IX_notifications_recipient]
        ON [common].[notifications] ([recipient_user_id], [created_at]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_notifications_status'
      AND [object_id] = OBJECT_ID(N'[common].[notifications]'))
    CREATE INDEX [IX_notifications_status]
        ON [common].[notifications] ([status])
        WHERE [status] = 'Pending';
GO

-- FR-COM-07: a retried outbox event must update its existing delivery row
-- rather than create a duplicate, so (outbox_message_id, channel) is unique.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_notifications_outbox_channel'
      AND [object_id] = OBJECT_ID(N'[common].[notifications]'))
    CREATE UNIQUE INDEX [UX_notifications_outbox_channel]
        ON [common].[notifications] ([outbox_message_id], [channel])
        WHERE [outbox_message_id] IS NOT NULL;
GO
