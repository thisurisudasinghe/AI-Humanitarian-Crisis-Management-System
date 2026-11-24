-- ============================================================================
-- 11_access_reporting.sql - staff access applications and guest reporting
-- ============================================================================
-- Run after 10_operations_centre.sql and before 98_foreign_keys.sql.
-- Plain idempotent SQL is the schema authority; EF migrations are not applied.
-- This script only adds objects and never drops or rewrites existing data.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF SCHEMA_ID(N'access') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [access]');
END;
GO

-- ----------------------------------------------------------------------------
-- Authenticated staff access applications.
-- Applicant-supplied text is stored encrypted by the application service.
-- The requested role is review data only until a reviewer approves it.
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[access].[staff_access_applications]', N'U') IS NULL
BEGIN
    CREATE TABLE [access].[staff_access_applications] (
        [id] uniqueidentifier NOT NULL,
        [applicant_user_id] uniqueidentifier NOT NULL,
        [reference] nvarchar(32) NOT NULL,
        [full_name] nvarchar(1000) NOT NULL,
        [email] nvarchar(1000) NOT NULL,
        [organization] nvarchar(1000) NOT NULL,
        [designation] nvarchar(1000) NOT NULL,
        [requested_role] nvarchar(100) NOT NULL,
        [reason] nvarchar(4000) NOT NULL,
        [status] nvarchar(40) NOT NULL CONSTRAINT [DF_staff_access_applications_status] DEFAULT (N'Pending'),
        [submitted_at] datetime2 NOT NULL,
        [reviewed_at] datetime2 NULL,
        [reviewer_user_id] uniqueidentifier NULL,
        [rejection_reason] nvarchar(4000) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_staff_access_applications] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_staff_access_applications_reference'
      AND [object_id] = OBJECT_ID(N'[access].[staff_access_applications]'))
BEGIN
    CREATE UNIQUE INDEX [UX_staff_access_applications_reference]
        ON [access].[staff_access_applications] ([reference]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_staff_access_applications_applicant_status'
      AND [object_id] = OBJECT_ID(N'[access].[staff_access_applications]'))
BEGIN
    CREATE INDEX [IX_staff_access_applications_applicant_status]
        ON [access].[staff_access_applications] ([applicant_user_id], [status]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_staff_access_applications_status'
      AND [object_id] = OBJECT_ID(N'[access].[staff_access_applications]'))
BEGIN
    CREATE INDEX [IX_staff_access_applications_status]
        ON [access].[staff_access_applications] ([status]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_staff_access_applications_status')
BEGIN
    ALTER TABLE [access].[staff_access_applications]
        ADD CONSTRAINT [CK_staff_access_applications_status]
        CHECK ([status] IN (N'Pending', N'Approved', N'Rejected'));
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_staff_access_applications_role')
BEGIN
    ALTER TABLE [access].[staff_access_applications]
        ADD CONSTRAINT [CK_staff_access_applications_role]
        CHECK ([requested_role] IN (
            N'EmergencyCoordinator', N'CaseManager', N'LogisticsManager',
            N'ShelterAdmin', N'VolunteerCoordinator', N'DonorOfficer',
            N'PartnerOfficer'));
END;
GO

-- ----------------------------------------------------------------------------
-- Anonymous emergency reports and public-safe tracking timeline.
-- Only a SHA-256 hash of the high-entropy bearer token is stored. Reports are
-- never published to the public map or the public alert feed.
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[access].[guest_reports]', N'U') IS NULL
BEGIN
    CREATE TABLE [access].[guest_reports] (
        [id] uniqueidentifier NOT NULL,
        [reference] nvarchar(32) NOT NULL,
        [token_hash] nvarchar(128) NOT NULL,
        [incident_type] nvarchar(100) NOT NULL,
        [location] nvarchar(500) NOT NULL,
        [latitude] decimal(18,7) NULL,
        [longitude] decimal(18,7) NULL,
        [description] nvarchar(4000) NOT NULL,
        [contact_info] nvarchar(2000) NULL,
        [status] nvarchar(40) NOT NULL CONSTRAINT [DF_guest_reports_status] DEFAULT (N'Received'),
        [submitted_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_guest_reports] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_guest_reports_reference'
      AND [object_id] = OBJECT_ID(N'[access].[guest_reports]'))
BEGIN
    CREATE UNIQUE INDEX [UX_guest_reports_reference]
        ON [access].[guest_reports] ([reference]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_guest_reports_token_hash'
      AND [object_id] = OBJECT_ID(N'[access].[guest_reports]'))
BEGIN
    CREATE UNIQUE INDEX [UX_guest_reports_token_hash]
        ON [access].[guest_reports] ([token_hash]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_guest_reports_status'
      AND [object_id] = OBJECT_ID(N'[access].[guest_reports]'))
BEGIN
    CREATE INDEX [IX_guest_reports_status] ON [access].[guest_reports] ([status]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_guest_reports_status')
BEGIN
    ALTER TABLE [access].[guest_reports]
        ADD CONSTRAINT [CK_guest_reports_status]
        CHECK ([status] IN (N'Received', N'UnderReview', N'Assigned', N'Resolved', N'Closed'));
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_guest_reports_latitude')
BEGIN
    ALTER TABLE [access].[guest_reports]
        ADD CONSTRAINT [CK_guest_reports_latitude] CHECK ([latitude] BETWEEN -90 AND 90);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_guest_reports_longitude')
BEGIN
    ALTER TABLE [access].[guest_reports]
        ADD CONSTRAINT [CK_guest_reports_longitude] CHECK ([longitude] BETWEEN -180 AND 180);
END;
GO

IF OBJECT_ID(N'[access].[guest_report_timeline]', N'U') IS NULL
BEGIN
    CREATE TABLE [access].[guest_report_timeline] (
        [id] uniqueidentifier NOT NULL,
        [report_id] uniqueidentifier NOT NULL,
        [status] nvarchar(40) NOT NULL,
        [note] nvarchar(4000) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_guest_report_timeline] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_guest_report_timeline_report_created'
      AND [object_id] = OBJECT_ID(N'[access].[guest_report_timeline]'))
BEGIN
    CREATE INDEX [IX_guest_report_timeline_report_created]
        ON [access].[guest_report_timeline] ([report_id], [created_at]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = N'CK_guest_report_timeline_status')
BEGIN
    ALTER TABLE [access].[guest_report_timeline]
        ADD CONSTRAINT [CK_guest_report_timeline_status]
        CHECK ([status] IN (N'Received', N'UnderReview', N'Assigned', N'Resolved', N'Closed'));
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_staff_access_applications_applicant')
BEGIN
    ALTER TABLE [access].[staff_access_applications]
        ADD CONSTRAINT [FK_staff_access_applications_applicant]
        FOREIGN KEY ([applicant_user_id]) REFERENCES [identity].[users] ([id]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_staff_access_applications_reviewer')
BEGIN
    ALTER TABLE [access].[staff_access_applications]
        ADD CONSTRAINT [FK_staff_access_applications_reviewer]
        FOREIGN KEY ([reviewer_user_id]) REFERENCES [identity].[users] ([id]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_guest_report_timeline_reports')
BEGIN
    ALTER TABLE [access].[guest_report_timeline]
        ADD CONSTRAINT [FK_guest_report_timeline_reports]
        FOREIGN KEY ([report_id]) REFERENCES [access].[guest_reports] ([id]) ON DELETE CASCADE;
END;
GO
