-- ============================================================================
-- 06_vol.sql - Member 4 / M7 volunteers
-- ============================================================================
-- Idempotent schema script. It creates only missing objects and never drops
-- data. Tables and columns use the same snake_case names as CmsDbContext after
-- NamingConventions.ApplySnakeCase.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'vol') IS NULL
    EXEC(N'CREATE SCHEMA [vol] AUTHORIZATION [dbo];');
GO

IF OBJECT_ID(N'[vol].[volunteers]', N'U') IS NULL
BEGIN
    CREATE TABLE [vol].[volunteers] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [skills_json] nvarchar(max) NOT NULL DEFAULT N'[]',
        [languages_json] nvarchar(max) NOT NULL DEFAULT N'[]',
        [verification_status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [active] bit NOT NULL DEFAULT 1,
        [lat] float NULL,
        [lng] float NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_vol_volunteers] PRIMARY KEY ([id]),
        CONSTRAINT [UQ_vol_volunteers_user] UNIQUE ([user_id])
    );
END;
GO

IF OBJECT_ID(N'[vol].[availability]', N'U') IS NULL
BEGIN
    CREATE TABLE [vol].[availability] (
        [id] uniqueidentifier NOT NULL,
        [volunteer_id] uniqueidentifier NOT NULL,
        [starts_at] datetime2 NOT NULL,
        [ends_at] datetime2 NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_vol_availability] PRIMARY KEY ([id]),
        CONSTRAINT [CK_vol_availability_window] CHECK ([ends_at] > [starts_at])
    );
END;
GO

IF OBJECT_ID(N'[vol].[tasks]', N'U') IS NULL
BEGIN
    CREATE TABLE [vol].[tasks] (
        [id] uniqueidentifier NOT NULL,
        [request_id] uniqueidentifier NULL,
        [title] nvarchar(160) NOT NULL,
        [required_skills_json] nvarchar(max) NOT NULL DEFAULT N'[]',
        [lat] float NULL,
        [lng] float NULL,
        [scheduled_at] datetime2 NULL,
        [status] nvarchar(20) NOT NULL DEFAULT N'Open',
        [volunteer_id] uniqueidentifier NULL,
        [completed_at] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_vol_tasks] PRIMARY KEY ([id])
    );
END;
GO

IF OBJECT_ID(N'[vol].[activities]', N'U') IS NULL
BEGIN
    CREATE TABLE [vol].[activities] (
        [id] uniqueidentifier NOT NULL,
        [volunteer_id] uniqueidentifier NOT NULL,
        [task_id] uniqueidentifier NOT NULL,
        [completed_by] uniqueidentifier NOT NULL,
        [completed_at] datetime2 NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_vol_activities] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_vol_availability_volunteer'
      AND [object_id] = OBJECT_ID(N'[vol].[availability]'))
    CREATE INDEX [IX_vol_availability_volunteer]
        ON [vol].[availability] ([volunteer_id], [starts_at], [ends_at]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_vol_tasks_volunteer'
      AND [object_id] = OBJECT_ID(N'[vol].[tasks]'))
    CREATE INDEX [IX_vol_tasks_volunteer]
        ON [vol].[tasks] ([volunteer_id], [status]);
GO
