-- ============================================================================
-- 05_shelter.sql - Member 4 / M6 Shelters
-- Run after 00_foundation.sql. Occupancy is written only by the guarded
-- allocation UPDATE; the CHECK constraint is defence in depth.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF SCHEMA_ID(N'shelter') IS NULL EXEC(N'CREATE SCHEMA [shelter] AUTHORIZATION [dbo];');
GO

IF OBJECT_ID(N'[shelter].[shelters]', N'U') IS NULL
BEGIN
    CREATE TABLE [shelter].[shelters] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(160) NOT NULL,
        [capacity] int NOT NULL,
        [occupancy] int NOT NULL DEFAULT 0,
        [facilities] nvarchar(max) NOT NULL DEFAULT N'[]',
        [accessibility_features] nvarchar(max) NOT NULL DEFAULT N'[]',
        [services] nvarchar(max) NOT NULL DEFAULT N'[]',
        [lat] float NOT NULL,
        [lng] float NOT NULL,
        [org_id] uniqueidentifier NOT NULL,
        [approved] bit NOT NULL DEFAULT 0,
        [created_at] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_shelter_shelters] PRIMARY KEY ([id]),
        CONSTRAINT [CK_shelter_capacity] CHECK ([capacity] > 0),
        CONSTRAINT [CK_shelter_occupancy] CHECK ([occupancy] >= 0 AND [occupancy] <= [capacity])
    );
END;
GO

IF OBJECT_ID(N'[shelter].[shelter_allocations]', N'U') IS NULL
BEGIN
    CREATE TABLE [shelter].[shelter_allocations] (
        [id] uniqueidentifier NOT NULL,
        [request_id] uniqueidentifier NOT NULL,
        [shelter_id] uniqueidentifier NOT NULL,
        [household_size] int NOT NULL,
        [decision_id] uniqueidentifier NOT NULL,
        [created_at] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_shelter_allocations] PRIMARY KEY ([id]),
        CONSTRAINT [CK_shelter_allocations_household_size] CHECK ([household_size] > 0)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_shelter_shelters_org' AND [object_id] = OBJECT_ID(N'[shelter].[shelters]'))
    CREATE INDEX [IX_shelter_shelters_org] ON [shelter].[shelters] ([org_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_shelter_shelters_approved' AND [object_id] = OBJECT_ID(N'[shelter].[shelters]'))
    CREATE INDEX [IX_shelter_shelters_approved] ON [shelter].[shelters] ([approved]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_shelter_allocations_shelter' AND [object_id] = OBJECT_ID(N'[shelter].[shelter_allocations]'))
    CREATE INDEX [IX_shelter_allocations_shelter] ON [shelter].[shelter_allocations] ([shelter_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_shelter_allocations_request' AND [object_id] = OBJECT_ID(N'[shelter].[shelter_allocations]'))
    CREATE INDEX [IX_shelter_allocations_request] ON [shelter].[shelter_allocations] ([request_id]);
GO
