-- ============================================================================
-- 09_donor_portal.sql - additive donor portal workflow support
-- ============================================================================
-- Extends the existing donation schema with optional, non-destructive fields
-- used by the donor portal. Existing rows and all other modules are untouched.
--
-- Run after 08_don.sql and before 98_foreign_keys.sql. Idempotent: missing
-- columns, tables, constraints and indexes are added; existing objects are
-- never dropped or recreated.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[don].[donations]', N'campaign_id') IS NULL
        ALTER TABLE [don].[donations] ADD [campaign_id] uniqueidentifier NULL;

    IF COL_LENGTH(N'[don].[donations]', N'category') IS NULL
        ALTER TABLE [don].[donations] ADD [category] nvarchar(80) NULL;

    IF COL_LENGTH(N'[don].[donations]', N'unit') IS NULL
        ALTER TABLE [don].[donations] ADD [unit] nvarchar(80) NULL;

    IF COL_LENGTH(N'[don].[donations]', N'location_text') IS NULL
        ALTER TABLE [don].[donations] ADD [location_text] nvarchar(300) NULL;

    IF COL_LENGTH(N'[don].[donations]', N'latitude') IS NULL
        ALTER TABLE [don].[donations] ADD [latitude] float NULL;

    IF COL_LENGTH(N'[don].[donations]', N'longitude') IS NULL
        ALTER TABLE [don].[donations] ADD [longitude] float NULL;

    IF COL_LENGTH(N'[don].[donations]', N'delivery_method') IS NULL
        ALTER TABLE [don].[donations] ADD [delivery_method] nvarchar(80) NULL;

    IF COL_LENGTH(N'[don].[donations]', N'idempotency_key') IS NULL
        ALTER TABLE [don].[donations] ADD [idempotency_key] nvarchar(100) NULL;
END;
GO

-- Align any earlier decimal columns with the CLR double mapping used by EF.
IF OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM sys.columns AS [c]
       INNER JOIN sys.types AS [t] ON [c].[user_type_id] = [t].[user_type_id]
       WHERE [c].[object_id] = OBJECT_ID(N'[don].[donations]')
         AND [c].[name] IN (N'latitude', N'longitude')
         AND [t].[name] IN (N'decimal', N'numeric'))
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[don].[donations]')
          AND [name] = N'CK_donations_latitude')
        ALTER TABLE [don].[donations] DROP CONSTRAINT [CK_donations_latitude];

    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[don].[donations]')
          AND [name] = N'CK_donations_longitude')
        ALTER TABLE [don].[donations] DROP CONSTRAINT [CK_donations_longitude];

    ALTER TABLE [don].[donations] ALTER COLUMN [latitude] float NULL;
    ALTER TABLE [don].[donations] ALTER COLUMN [longitude] float NULL;
END;
GO

-- Guard coordinate values even when rows are written outside the API.
IF OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[don].[donations]')
          AND [name] = N'CK_donations_latitude')
BEGIN
    ALTER TABLE [don].[donations] WITH CHECK
        ADD CONSTRAINT [CK_donations_latitude]
        CHECK ([latitude] IS NULL OR ([latitude] >= -90 AND [latitude] <= 90));
END;
GO

IF OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[don].[donations]')
          AND [name] = N'CK_donations_longitude')
BEGIN
    ALTER TABLE [don].[donations] WITH CHECK
        ADD CONSTRAINT [CK_donations_longitude]
        CHECK ([longitude] IS NULL OR ([longitude] >= -180 AND [longitude] <= 180));
END;
GO

IF OBJECT_ID(N'[don].[campaigns]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE [parent_object_id] = OBJECT_ID(N'[don].[donations]')
          AND [name] = N'FK_donations_campaigns_portal')
BEGIN
    ALTER TABLE [don].[donations] WITH CHECK
        ADD CONSTRAINT [FK_donations_campaigns_portal]
        FOREIGN KEY ([campaign_id]) REFERENCES [don].[campaigns] ([id]) ON DELETE NO ACTION;

    ALTER TABLE [don].[donations] CHECK CONSTRAINT [FK_donations_campaigns_portal];
END;
GO

IF OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.indexes
       WHERE [name] = N'UX_donations_idempotency_key'
         AND [object_id] = OBJECT_ID(N'[don].[donations]'))
BEGIN
    CREATE UNIQUE INDEX [UX_donations_idempotency_key]
        ON [don].[donations] ([idempotency_key])
        WHERE [idempotency_key] IS NOT NULL;
END;
GO

IF OBJECT_ID(N'[don].[donations]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.indexes
       WHERE [name] = N'IX_donations_campaign_id'
         AND [object_id] = OBJECT_ID(N'[don].[donations]'))
BEGIN
    CREATE INDEX [IX_donations_campaign_id]
        ON [don].[donations] ([campaign_id]);
END;
GO

-- ----------------------------------------------------------------------------
-- Structured material donation items
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[don].[donation_items]', N'U') IS NULL
BEGIN
    CREATE TABLE [don].[donation_items] (
        [id] uniqueidentifier NOT NULL,
        [donation_id] uniqueidentifier NOT NULL,
        [name] nvarchar(200) NOT NULL,
        [category] nvarchar(80) NULL,
        [quantity] int NOT NULL,
        [unit] nvarchar(80) NULL,
        [item_condition] nvarchar(100) NULL,
        [notes] nvarchar(500) NULL,
        [expiry_date] date NULL,
        [packaging] nvarchar(200) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_donation_items] PRIMARY KEY ([id]),
        CONSTRAINT [FK_donation_items_donations] FOREIGN KEY ([donation_id])
            REFERENCES [don].[donations] ([id]) ON DELETE CASCADE,
        CONSTRAINT [CK_donation_items_quantity] CHECK ([quantity] > 0)
    );
END;
GO

IF OBJECT_ID(N'[don].[donation_items]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.indexes
       WHERE [name] = N'IX_donation_items_donation_id'
         AND [object_id] = OBJECT_ID(N'[don].[donation_items]'))
BEGIN
    CREATE INDEX [IX_donation_items_donation_id]
        ON [don].[donation_items] ([donation_id]);
END;
GO
