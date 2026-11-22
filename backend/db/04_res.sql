-- ============================================================================
-- 04_res.sql - Member 4 / M5 Resources
-- Run after 00_foundation.sql. Tables and columns are snake_case to match the
-- EF runtime model produced by NamingConventions.ApplySnakeCase.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF SCHEMA_ID(N'res') IS NULL EXEC(N'CREATE SCHEMA [res] AUTHORIZATION [dbo];');
GO

IF OBJECT_ID(N'[res].[resource_types]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[resource_types] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(120) NOT NULL,
        [unit] nvarchar(40) NOT NULL,
        [description] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_resource_types] PRIMARY KEY ([id]),
        CONSTRAINT [UQ_res_resource_types_name] UNIQUE ([name])
    );
END;
GO

IF OBJECT_ID(N'[res].[inventories]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[inventories] (
        [id] uniqueidentifier NOT NULL,
        [resource_type_id] uniqueidentifier NOT NULL,
        [org_id] uniqueidentifier NOT NULL,
        [quantity] decimal(18,2) NOT NULL,
        [available] decimal(18,2) NOT NULL,
        [reserved] decimal(18,2) NOT NULL DEFAULT 0,
        [allocated] decimal(18,2) NOT NULL DEFAULT 0,
        [dispatched] decimal(18,2) NOT NULL DEFAULT 0,
        [utilised] decimal(18,2) NOT NULL DEFAULT 0,
        [reorder_threshold] decimal(18,2) NOT NULL DEFAULT 0,
        [expiry_date] datetime2 NULL,
        [lat] float NULL,
        [lng] float NULL,
        [accessibility_capable] bit NOT NULL DEFAULT 0,
        [created_at] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_inventories] PRIMARY KEY ([id]),
        CONSTRAINT [CK_res_inventories_non_negative] CHECK (
            [quantity] >= 0 AND [available] >= 0 AND [reserved] >= 0 AND
            [allocated] >= 0 AND [dispatched] >= 0 AND [utilised] >= 0)
    );
END;
GO

IF OBJECT_ID(N'[res].[allocations]', N'U') IS NULL
BEGIN
    CREATE TABLE [res].[allocations] (
        [id] uniqueidentifier NOT NULL,
        [request_id] uniqueidentifier NOT NULL,
        [inventory_id] uniqueidentifier NOT NULL,
        [quantity] decimal(18,2) NOT NULL,
        [decision_id] uniqueidentifier NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [explanation] nvarchar(1000) NOT NULL,
        [idempotency_key] nvarchar(256) NOT NULL,
        [created_at] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
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
        [decision_id] uniqueidentifier NOT NULL,
        [quantity] decimal(18,2) NOT NULL,
        [confirmed_at] datetime2 NULL,
        [confirmed_by] uniqueidentifier NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_dispatches] PRIMARY KEY ([id]),
        CONSTRAINT [UQ_res_dispatches_decision] UNIQUE ([decision_id])
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
        [quantity] decimal(18,2) NOT NULL,
        [reason] nvarchar(1000) NOT NULL,
        [status] nvarchar(20) NOT NULL,
        [shelter_id] uniqueidentifier NULL,
        [response_reason] nvarchar(1000) NULL,
        [created_at] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_res_assistance_requests] PRIMARY KEY ([id])
    );
END;
GO

-- Indexes required by the resource lookup, allocation guard and status filters.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_inventories_org' AND [object_id] = OBJECT_ID(N'[res].[inventories]'))
    CREATE INDEX [IX_res_inventories_org] ON [res].[inventories] ([org_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_inventories_resource_type' AND [object_id] = OBJECT_ID(N'[res].[inventories]'))
    CREATE INDEX [IX_res_inventories_resource_type] ON [res].[inventories] ([resource_type_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_inventories_available' AND [object_id] = OBJECT_ID(N'[res].[inventories]'))
    CREATE INDEX [IX_res_inventories_available] ON [res].[inventories] ([available]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_allocations_request' AND [object_id] = OBJECT_ID(N'[res].[allocations]'))
    CREATE INDEX [IX_res_allocations_request] ON [res].[allocations] ([request_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_allocations_inventory' AND [object_id] = OBJECT_ID(N'[res].[allocations]'))
    CREATE INDEX [IX_res_allocations_inventory] ON [res].[allocations] ([inventory_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_allocations_status' AND [object_id] = OBJECT_ID(N'[res].[allocations]'))
    CREATE INDEX [IX_res_allocations_status] ON [res].[allocations] ([status]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_dispatches_allocation' AND [object_id] = OBJECT_ID(N'[res].[dispatches]'))
    CREATE INDEX [IX_res_dispatches_allocation] ON [res].[dispatches] ([allocation_id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_assistance_from_status' AND [object_id] = OBJECT_ID(N'[res].[assistance_requests]'))
    CREATE INDEX [IX_res_assistance_from_status] ON [res].[assistance_requests] ([from_org_id], [status]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_res_assistance_to_status' AND [object_id] = OBJECT_ID(N'[res].[assistance_requests]'))
    CREATE INDEX [IX_res_assistance_to_status] ON [res].[assistance_requests] ([to_org_id], [status]);
GO
