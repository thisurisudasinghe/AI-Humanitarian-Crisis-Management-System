-- ============================================================================
-- 10_operations_centre.sql - additive public live-map publication layer
-- ============================================================================
-- Extends cases.alerts and cases.service_centres with an explicit publication
-- layer for the anonymous public map. Existing rows keep their current
-- behaviour: alerts default to hidden, service centres default to public.
--
-- Run after 09_donor_portal.sql and before 98_foreign_keys.sql. Idempotent:
-- missing columns, constraints and indexes are added; existing objects are
-- never dropped, recreated or reset. No data is deleted or rewritten.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ----------------------------------------------------------------------------
-- Public alert projection. Private title/body remain untouched.
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[cases].[alerts]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[cases].[alerts]', N'public_visible') IS NULL
        ALTER TABLE [cases].[alerts] ADD [public_visible] bit NOT NULL
            CONSTRAINT [DF_alerts_public_visible] DEFAULT 0;

    IF COL_LENGTH(N'[cases].[alerts]', N'public_title') IS NULL
        ALTER TABLE [cases].[alerts] ADD [public_title] nvarchar(200) NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'public_summary') IS NULL
        ALTER TABLE [cases].[alerts] ADD [public_summary] nvarchar(1000) NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'public_type') IS NULL
        ALTER TABLE [cases].[alerts] ADD [public_type] nvarchar(40) NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'severity') IS NULL
        ALTER TABLE [cases].[alerts] ADD [severity] nvarchar(30) NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'district') IS NULL
        ALTER TABLE [cases].[alerts] ADD [district] nvarchar(120) NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'latitude') IS NULL
        ALTER TABLE [cases].[alerts] ADD [latitude] float NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'longitude') IS NULL
        ALTER TABLE [cases].[alerts] ADD [longitude] float NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'public_expires_at') IS NULL
        ALTER TABLE [cases].[alerts] ADD [public_expires_at] datetime2 NULL;

    IF COL_LENGTH(N'[cases].[alerts]', N'published_by_user_id') IS NULL
        ALTER TABLE [cases].[alerts] ADD [published_by_user_id] uniqueidentifier NULL;
END;
GO

IF OBJECT_ID(N'[cases].[alerts]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[cases].[alerts]')
          AND [name] = N'CK_alerts_latitude')
BEGIN
    ALTER TABLE [cases].[alerts] WITH CHECK
        ADD CONSTRAINT [CK_alerts_latitude]
        CHECK ([latitude] IS NULL OR ([latitude] >= -90 AND [latitude] <= 90));
END;
GO

IF OBJECT_ID(N'[cases].[alerts]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[cases].[alerts]')
          AND [name] = N'CK_alerts_longitude')
BEGIN
    ALTER TABLE [cases].[alerts] WITH CHECK
        ADD CONSTRAINT [CK_alerts_longitude]
        CHECK ([longitude] IS NULL OR ([longitude] >= -180 AND [longitude] <= 180));
END;
GO

IF OBJECT_ID(N'[cases].[alerts]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_alerts_public_map'
          AND [object_id] = OBJECT_ID(N'[cases].[alerts]'))
BEGIN
    CREATE INDEX [IX_alerts_public_map]
        ON [cases].[alerts] ([public_visible], [status], [public_expires_at])
        WHERE [public_visible] = 1;
END;
GO

-- ----------------------------------------------------------------------------
-- Service centres keep their existing public listing by default.
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[cases].[service_centres]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[cases].[service_centres]', N'public_visible') IS NULL
        ALTER TABLE [cases].[service_centres] ADD [public_visible] bit NOT NULL
            CONSTRAINT [DF_service_centres_public_visible] DEFAULT 1;
END;
GO

IF OBJECT_ID(N'[cases].[service_centres]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_service_centres_public'
          AND [object_id] = OBJECT_ID(N'[cases].[service_centres]'))
BEGIN
    CREATE INDEX [IX_service_centres_public]
        ON [cases].[service_centres] ([public_visible], [is_active])
        WHERE [public_visible] = 1;
END;
GO
