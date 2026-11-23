-- ============================================================================
-- 08_don.sql - Dev 1 donation management schema (M8, lean)
-- ============================================================================
-- Creates the donation tables owned by Dev 1:
--   don.donors, don.campaigns, don.donations,
--   don.donation_allocations, don.campaign_expenses
--
-- Run after 00_common.sql and before 98_foreign_keys.sql.
-- The script is idempotent: missing schemas, tables, indexes and foreign keys
-- are created; existing objects are left untouched. It never drops data.
--
-- Links to identity.users are guarded so this script also runs on its own
-- without 00_common.sql.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'don') IS NULL
    EXEC(N'CREATE SCHEMA [don] AUTHORIZATION [dbo];');
GO

-- ----------------------------------------------------------------------------
-- don.donors
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[don].[donors]', N'U') IS NULL
BEGIN
    CREATE TABLE [don].[donors] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(200) NOT NULL,
        [donor_type] nvarchar(30) NOT NULL,
        [contact_email] nvarchar(320) NULL,
        [contact_phone] nvarchar(20) NULL,
        [is_anonymous] bit NOT NULL,
        [is_active] bit NOT NULL,
        [user_id] uniqueidentifier NULL,
        [created_by_user_id] uniqueidentifier NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_donors] PRIMARY KEY ([id])
    );
END;
GO

-- Safe incremental upgrade for existing databases (never drops data).
IF COL_LENGTH(N'[don].[donors]', N'user_id') IS NULL
BEGIN
    ALTER TABLE [don].[donors] ADD [user_id] uniqueidentifier NULL;
END;
GO

IF COL_LENGTH(N'[don].[donors]', N'user_id') IS NOT NULL
   AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[don].[donors]')
      AND [name] = N'IX_donors_user_id')
BEGIN
    CREATE UNIQUE INDEX [IX_donors_user_id]
        ON [don].[donors] ([user_id])
        WHERE [user_id] IS NOT NULL;
END;
GO

-- ----------------------------------------------------------------------------
-- don.campaigns
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[don].[campaigns]', N'U') IS NULL
BEGIN
    CREATE TABLE [don].[campaigns] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(200) NOT NULL,
        [description] nvarchar(2000) NULL,
        [goal_amount] decimal(18, 2) NOT NULL,
        [currency] nvarchar(3) NOT NULL,
        [starts_at] datetime2 NOT NULL,
        [ends_at] datetime2 NULL,
        [status] nvarchar(30) NOT NULL,
        [created_by_user_id] uniqueidentifier NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_campaigns] PRIMARY KEY ([id]),
        CONSTRAINT [CK_campaigns_goal_amount] CHECK ([goal_amount] >= 0)
    );
END;
GO

-- ----------------------------------------------------------------------------
-- don.donations
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[don].[donations]', N'U') IS NULL
BEGIN
    CREATE TABLE [don].[donations] (
        [id] uniqueidentifier NOT NULL,
        [reference] nvarchar(40) NOT NULL,
        [donor_id] uniqueidentifier NOT NULL,
        [donation_type] nvarchar(30) NOT NULL,
        [amount] decimal(18, 2) NULL,
        [currency] nvarchar(3) NOT NULL,
        [item_description] nvarchar(500) NULL,
        [quantity] int NULL,
        [estimated_value] decimal(18, 2) NULL,
        [received_at] datetime2 NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [is_suspicious] bit NOT NULL,
        [suspicious_reason] nvarchar(500) NULL,
        [notes] nvarchar(1000) NULL,
        [recorded_by_user_id] uniqueidentifier NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_donations] PRIMARY KEY ([id]),
        CONSTRAINT [FK_donations_donors] FOREIGN KEY ([donor_id])
            REFERENCES [don].[donors] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_donations_amount] CHECK ([amount] IS NULL OR [amount] > 0),
        CONSTRAINT [CK_donations_estimated_value]
            CHECK ([estimated_value] IS NULL OR [estimated_value] >= 0),
        CONSTRAINT [CK_donations_quantity]
            CHECK ([quantity] IS NULL OR [quantity] > 0)
    );
END;
GO

-- ----------------------------------------------------------------------------
-- don.donation_allocations
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[don].[donation_allocations]', N'U') IS NULL
BEGIN
    CREATE TABLE [don].[donation_allocations] (
        [id] uniqueidentifier NOT NULL,
        [donation_id] uniqueidentifier NOT NULL,
        [campaign_id] uniqueidentifier NOT NULL,
        [amount] decimal(18, 2) NOT NULL,
        [note] nvarchar(500) NULL,
        [is_suspicious] bit NOT NULL,
        [suspicious_reason] nvarchar(500) NULL,
        [allocated_by_user_id] uniqueidentifier NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_donation_allocations] PRIMARY KEY ([id]),
        CONSTRAINT [FK_donation_allocations_donations] FOREIGN KEY ([donation_id])
            REFERENCES [don].[donations] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_donation_allocations_campaigns] FOREIGN KEY ([campaign_id])
            REFERENCES [don].[campaigns] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_donation_allocations_amount] CHECK ([amount] > 0)
    );
END;
GO

-- ----------------------------------------------------------------------------
-- don.campaign_expenses
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[don].[campaign_expenses]', N'U') IS NULL
BEGIN
    CREATE TABLE [don].[campaign_expenses] (
        [id] uniqueidentifier NOT NULL,
        [campaign_id] uniqueidentifier NOT NULL,
        [description] nvarchar(200) NOT NULL,
        [amount] decimal(18, 2) NOT NULL,
        [currency] nvarchar(3) NOT NULL,
        [spent_at] datetime2 NOT NULL,
        [recorded_by_user_id] uniqueidentifier NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_campaign_expenses] PRIMARY KEY ([id]),
        CONSTRAINT [FK_campaign_expenses_campaigns] FOREIGN KEY ([campaign_id])
            REFERENCES [don].[campaigns] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_campaign_expenses_amount] CHECK ([amount] > 0)
    );
END;
GO

-- ----------------------------------------------------------------------------
-- Guarded links to identity.users (owned by Dev 1, created by 00_common.sql)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[identity].[users]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[don].[donors]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_donors_users')
BEGIN
    ALTER TABLE [don].[donors] WITH CHECK
        ADD CONSTRAINT [FK_donors_users]
        FOREIGN KEY ([created_by_user_id]) REFERENCES [identity].[users] ([id])
        ON DELETE NO ACTION;

    ALTER TABLE [don].[donors] CHECK CONSTRAINT [FK_donors_users];
END;
GO

IF OBJECT_ID(N'[identity].[users]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_donations_users')
BEGIN
    ALTER TABLE [don].[donations] WITH CHECK
        ADD CONSTRAINT [FK_donations_users]
        FOREIGN KEY ([recorded_by_user_id]) REFERENCES [identity].[users] ([id])
        ON DELETE NO ACTION;

    ALTER TABLE [don].[donations] CHECK CONSTRAINT [FK_donations_users];
END;
GO

IF OBJECT_ID(N'[identity].[users]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[don].[donation_allocations]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_donation_allocations_users')
BEGIN
    ALTER TABLE [don].[donation_allocations] WITH CHECK
        ADD CONSTRAINT [FK_donation_allocations_users]
        FOREIGN KEY ([allocated_by_user_id]) REFERENCES [identity].[users] ([id])
        ON DELETE NO ACTION;

    ALTER TABLE [don].[donation_allocations] CHECK CONSTRAINT [FK_donation_allocations_users];
END;
GO

IF OBJECT_ID(N'[identity].[users]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[don].[campaigns]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_campaigns_users')
BEGIN
    ALTER TABLE [don].[campaigns] WITH CHECK
        ADD CONSTRAINT [FK_campaigns_users]
        FOREIGN KEY ([created_by_user_id]) REFERENCES [identity].[users] ([id])
        ON DELETE NO ACTION;

    ALTER TABLE [don].[campaigns] CHECK CONSTRAINT [FK_campaigns_users];
END;
GO

IF OBJECT_ID(N'[identity].[users]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[don].[campaign_expenses]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_campaign_expenses_users')
BEGIN
    ALTER TABLE [don].[campaign_expenses] WITH CHECK
        ADD CONSTRAINT [FK_campaign_expenses_users]
        FOREIGN KEY ([recorded_by_user_id]) REFERENCES [identity].[users] ([id])
        ON DELETE NO ACTION;

    ALTER TABLE [don].[campaign_expenses] CHECK CONSTRAINT [FK_campaign_expenses_users];
END;
GO

-- ----------------------------------------------------------------------------
-- Indexes
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donors_name'
      AND [object_id] = OBJECT_ID(N'[don].[donors]'))
    CREATE INDEX [IX_donors_name] ON [don].[donors] ([name]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donors_is_active'
      AND [object_id] = OBJECT_ID(N'[don].[donors]'))
    CREATE INDEX [IX_donors_is_active] ON [don].[donors] ([is_active]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_campaigns_status'
      AND [object_id] = OBJECT_ID(N'[don].[campaigns]'))
    CREATE INDEX [IX_campaigns_status] ON [don].[campaigns] ([status]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_campaigns_starts_at'
      AND [object_id] = OBJECT_ID(N'[don].[campaigns]'))
    CREATE INDEX [IX_campaigns_starts_at] ON [don].[campaigns] ([starts_at]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_donations_reference'
      AND [object_id] = OBJECT_ID(N'[don].[donations]'))
    CREATE UNIQUE INDEX [UX_donations_reference] ON [don].[donations] ([reference]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donations_donor_id'
      AND [object_id] = OBJECT_ID(N'[don].[donations]'))
    CREATE INDEX [IX_donations_donor_id] ON [don].[donations] ([donor_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donations_received_at'
      AND [object_id] = OBJECT_ID(N'[don].[donations]'))
    CREATE INDEX [IX_donations_received_at] ON [don].[donations] ([received_at]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donations_status'
      AND [object_id] = OBJECT_ID(N'[don].[donations]'))
    CREATE INDEX [IX_donations_status] ON [don].[donations] ([status]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donations_is_suspicious'
      AND [object_id] = OBJECT_ID(N'[don].[donations]'))
    CREATE INDEX [IX_donations_is_suspicious] ON [don].[donations] ([is_suspicious]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donation_allocations_donation_id'
      AND [object_id] = OBJECT_ID(N'[don].[donation_allocations]'))
    CREATE INDEX [IX_donation_allocations_donation_id]
        ON [don].[donation_allocations] ([donation_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_donation_allocations_campaign_id'
      AND [object_id] = OBJECT_ID(N'[don].[donation_allocations]'))
    CREATE INDEX [IX_donation_allocations_campaign_id]
        ON [don].[donation_allocations] ([campaign_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_campaign_expenses_campaign_id'
      AND [object_id] = OBJECT_ID(N'[don].[campaign_expenses]'))
    CREATE INDEX [IX_campaign_expenses_campaign_id]
        ON [don].[campaign_expenses] ([campaign_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_campaign_expenses_spent_at'
      AND [object_id] = OBJECT_ID(N'[don].[campaign_expenses]'))
    CREATE INDEX [IX_campaign_expenses_spent_at]
        ON [don].[campaign_expenses] ([spent_at]);
GO
