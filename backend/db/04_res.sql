-- ============================================================================
-- 04_res.sql - Member 4 / M5 resources and logistics
-- ============================================================================
-- Idempotent schema script. It creates only missing objects and never drops
-- data. Tables and columns use the same snake_case names as CmsDbContext after
-- NamingConventions.ApplySnakeCase.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'res') IS NULL
    EXEC(N'CREATE SCHEMA [res] AUTHORIZATION [dbo];');
GO

IF OBJECT_ID(N'[res].[resource_types]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[resource_types] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(120) NOT NULL,
        [unit] nvarchar(40) NOT NULL,
        [description] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_resource_types] PRIMARY KEY ([id])
    );
END;
GO

IF OBJECT_ID(N'[res].[inventories]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[inventories] (
        [id] uniqueidentifier NOT NULL,
        [resource_type_id] uniqueidentifier NOT NULL,
        [org_id] uniqueidentifier NOT NULL,
        [quantity] decimal(18, 2) NOT NULL,
        [available] decimal(18, 2) NOT NULL,
        [reserved] decimal(18, 2) NOT NULL DEFAULT 0,
        [allocated] decimal(18, 2) NOT NULL DEFAULT 0,
        [dispatched] decimal(18, 2) NOT NULL DEFAULT 0,
        [utilised] decimal(18, 2) NOT NULL DEFAULT 0,
        [reorder_threshold] decimal(18, 2) NOT NULL DEFAULT 0,
        [expiry_date] datetime2 NULL,
        [lat] float NULL,
        [lng] float NULL,
        [accessibility_capable] bit NOT NULL DEFAULT 0,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_inventories] PRIMARY KEY ([id]),
        CONSTRAINT [CK_res_inventories_non_negative]
            CHECK ([available] >= 0 AND [reserved] >= 0 AND [allocated] >= 0
                   AND [dispatched] >= 0 AND [utilised] >= 0)
    );
END;
GO

IF OBJECT_ID(N'[res].[allocations]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[allocations] (
        [id] uniqueidentifier NOT NULL,
        [request_id] uniqueidentifier NOT NULL,
        [inventory_id] uniqueidentifier NOT NULL,
        [quantity] decimal(18, 2) NOT NULL,
        [decision_id] uniqueidentifier NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [explanation] nvarchar(1000) NOT NULL,
        [idempotency_key] nvarchar(200) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_allocations] PRIMARY KEY ([id]),
        CONSTRAINT [UQ_res_allocations_idempotency_key] UNIQUE ([idempotency_key])
    );
END;
GO

IF OBJECT_ID(N'[res].[dispatches]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[dispatches] (
        [id] uniqueidentifier NOT NULL,
        [allocation_id] uniqueidentifier NOT NULL,
        [quantity] decimal(18, 2) NOT NULL,
        [confirmed_at] datetime2 NULL,
        [confirmed_by] uniqueidentifier NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_dispatches] PRIMARY KEY ([id])
    );
END;
GO

IF OBJECT_ID(N'[res].[assistance_requests]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[assistance_requests] (
        [id] uniqueidentifier NOT NULL,
        [from_org_id] uniqueidentifier NOT NULL,
        [to_org_id] uniqueidentifier NULL,
        [resource_type_id] uniqueidentifier NOT NULL,
        [quantity] decimal(18, 2) NOT NULL,
        [reason] nvarchar(1000) NOT NULL,
        [status] nvarchar(20) NOT NULL,
        [shelter_id] uniqueidentifier NULL,
        [response_reason] nvarchar(1000) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_assistance_requests] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_res_inventories_resource_type'
      AND [object_id] = OBJECT_ID(N'[res].[inventories]'))
    CREATE INDEX [IX_res_inventories_resource_type]
        ON [res].[inventories] ([resource_type_id]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_res_allocations_request'
      AND [object_id] = OBJECT_ID(N'[res].[allocations]'))
    CREATE INDEX [IX_res_allocations_request]
        ON [res].[allocations] ([request_id]);
GO
