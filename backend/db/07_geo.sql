-- ============================================================================
-- 91_seed_ops.sql - Member 4 M5/M6/M7 demonstration data
-- Run after 90_seed_scenario.sql. Fixed GUIDs match the three urgent Food
-- requests reserved by Member 2. Every row guard is per-row so a partially
-- failed earlier run can be completed safely.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @LeadAgencyOrgId UNIQUEIDENTIFIER = '0f000000-0000-0000-0000-000000000001';

IF OBJECT_ID(N'[res].[resource_types]', N'U') IS NULL OR OBJECT_ID(N'[res].[inventories]', N'U') IS NULL
BEGIN
    THROW 50091, N'Missing Member 4 schemas: run 04_res.sql, 05_shelter.sql and 06_vol.sql first.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM [res].[resource_types] WHERE [id] = '41000000-0000-0000-0000-000000000001')
    INSERT [res].[resource_types] ([id], [name], [unit], [description], [created_at])
    VALUES ('41000000-0000-0000-0000-000000000001', N'Emergency food pack', N'packs', N'Deliberately limited demo stock for the three urgent Food requests', SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [res].[resource_types] WHERE [id] = '41000000-0000-0000-0000-000000000002')
    INSERT [res].[resource_types] ([id], [name], [unit], [description], [created_at])
    VALUES ('41000000-0000-0000-0000-000000000002', N'Medical kit', N'kits', N'General first aid kit', SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [res].[resource_types] WHERE [id] = '41000000-0000-0000-0000-000000000003')
    INSERT [res].[resource_types] ([id], [name], [unit], [description], [created_at])
    VALUES ('41000000-0000-0000-0000-000000000003', N'Clean water', N'litres', N'Drinking water supply', SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [res].[inventories] WHERE [id] = '42000000-0000-0000-0000-000000000001')
    INSERT [res].[inventories] ([id], [resource_type_id], [org_id], [quantity], [available], [reserved], [allocated], [dispatched], [utilised], [reorder_threshold], [lat], [lng], [accessibility_capable], [created_at])
    VALUES ('42000000-0000-0000-0000-000000000001', '41000000-0000-0000-0000-000000000001', '0f000000-0000-0000-0000-000000000001', 5, 5, 0, 0, 0, 0, 2, 7.2088, 79.8358, 0, SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [res].[inventories] WHERE [id] = '42000000-0000-0000-0000-000000000002')
    INSERT [res].[inventories] ([id], [resource_type_id], [org_id], [quantity], [available], [reserved], [allocated], [dispatched], [utilised], [reorder_threshold], [lat], [lng], [accessibility_capable], [created_at])
    VALUES ('42000000-0000-0000-0000-000000000002', '41000000-0000-0000-0000-000000000002', '0f000000-0000-0000-0000-000000000001', 30, 30, 0, 0, 0, 0, 5, 7.2088, 79.8358, 1, SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [res].[inventories] WHERE [id] = '42000000-0000-0000-0000-000000000003')
    INSERT [res].[inventories] ([id], [resource_type_id], [org_id], [quantity], [available], [reserved], [allocated], [dispatched], [utilised], [reorder_threshold], [lat], [lng], [accessibility_capable], [created_at])
    VALUES ('42000000-0000-0000-0000-000000000003', '41000000-0000-0000-0000-000000000003', '0f000000-0000-0000-0000-000000000001', 100, 100, 0, 0, 0, 0, 20, 7.2088, 79.8358, 1, SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [shelter].[shelters] WHERE [id] = '44000000-0000-0000-0000-000000000001')
    INSERT [shelter].[shelters] ([id], [name], [capacity], [occupancy], [facilities], [accessibility_features], [services], [lat], [lng], [org_id], [approved], [created_at])
    VALUES ('44000000-0000-0000-0000-000000000001', N'Negombo Community Hall', 120, 40, N'["Water","Sanitation"]', N'["Wheelchair access"]', N'["Food","First aid"]', 7.2088, 79.8358, '0f000000-0000-0000-0000-000000000001', 1, SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [shelter].[shelters] WHERE [id] = '44000000-0000-0000-0000-000000000002')
    INSERT [shelter].[shelters] ([id], [name], [capacity], [occupancy], [facilities], [accessibility_features], [services], [lat], [lng], [org_id], [approved], [created_at])
    VALUES ('44000000-0000-0000-0000-000000000002', N'Ja-Ela Relief Centre', 80, 22, N'["Water","Power"]', N'["Accessible toilets"]', N'["Food","Shelter"]', 7.0744, 79.8919, '0f000000-0000-0000-0000-000000000001', 1, SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [shelter].[shelters] WHERE [id] = '44000000-0000-0000-0000-000000000003')
    INSERT [shelter].[shelters] ([id], [name], [capacity], [occupancy], [facilities], [accessibility_features], [services], [lat], [lng], [org_id], [approved], [created_at])
    VALUES ('44000000-0000-0000-0000-000000000003', N'Wattala Temporary Shelter', 60, 60, N'["Water"]', N'[]', N'["Shelter"]', 6.9890, 79.8910, '0f000000-0000-0000-0000-000000000001', 1, SYSUTCDATETIME());
GO

IF NOT EXISTS (SELECT 1 FROM [vol].[volunteers] WHERE [id] = '45000000-0000-0000-0000-000000000001')
    INSERT [vol].[volunteers] ([id], [user_id], [skills_json], [languages_json], [verification_status], [active], [lat], [lng], [created_at])
    VALUES ('45000000-0000-0000-0000-000000000001', '45100000-0000-0000-0000-000000000001', N'["First aid","Driving"]', N'["English","Sinhala"]', N'Verified', 1, 7.2088, 79.8358, SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [vol].[volunteers] WHERE [id] = '45000000-0000-0000-0000-000000000002')
    INSERT [vol].[volunteers] ([id], [user_id], [skills_json], [languages_json], [verification_status], [active], [lat], [lng], [created_at])
    VALUES ('45000000-0000-0000-0000-000000000002', '45100000-0000-0000-0000-000000000002', N'["Food distribution"]', N'["Sinhala"]', N'Verified', 1, 7.0744, 79.8919, SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [vol].[volunteers] WHERE [id] = '45000000-0000-0000-0000-000000000003')
    INSERT [vol].[volunteers] ([id], [user_id], [skills_json], [languages_json], [verification_status], [active], [lat], [lng], [created_at])
    VALUES ('45000000-0000-0000-0000-000000000003', '45100000-0000-0000-0000-000000000003', N'["Medical support"]', N'["English"]', N'Verified', 1, 6.9890, 79.8910, SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [vol].[volunteers] WHERE [id] = '45000000-0000-0000-0000-000000000004')
    INSERT [vol].[volunteers] ([id], [user_id], [skills_json], [languages_json], [verification_status], [active], [lat], [lng], [created_at])
    VALUES ('45000000-0000-0000-0000-000000000004', '45100000-0000-0000-0000-000000000004', N'["Logistics"]', N'["Sinhala","Tamil"]', N'Pending', 1, 7.2088, 79.8358, SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [vol].[volunteers] WHERE [id] = '45000000-0000-0000-0000-000000000005')
    INSERT [vol].[volunteers] ([id], [user_id], [skills_json], [languages_json], [verification_status], [active], [lat], [lng], [created_at])
    VALUES ('45000000-0000-0000-0000-000000000005', '45100000-0000-0000-0000-000000000005', N'["Shelter support"]', N'["Tamil"]', N'Verified', 1, 7.0744, 79.8919, SYSUTCDATETIME());
GO

-- Availability windows are required by IVolunteerMatcher and task assignment.
IF NOT EXISTS (SELECT 1 FROM [vol].[availability] WHERE [id] = '46000000-0000-0000-0000-000000000001')
    INSERT [vol].[availability] ([id], [volunteer_id], [starts_at], [ends_at], [created_at])
    VALUES ('46000000-0000-0000-0000-000000000001', '45000000-0000-0000-0000-000000000001', DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 30, SYSUTCDATETIME()), SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [vol].[availability] WHERE [id] = '46000000-0000-0000-0000-000000000002')
    INSERT [vol].[availability] ([id], [volunteer_id], [starts_at], [ends_at], [created_at])
    VALUES ('46000000-0000-0000-0000-000000000002', '45000000-0000-0000-0000-000000000002', DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 30, SYSUTCDATETIME()), SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [vol].[availability] WHERE [id] = '46000000-0000-0000-0000-000000000003')
    INSERT [vol].[availability] ([id], [volunteer_id], [starts_at], [ends_at], [created_at])
    VALUES ('46000000-0000-0000-0000-000000000003', '45000000-0000-0000-0000-000000000003', DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 30, SYSUTCDATETIME()), SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [vol].[availability] WHERE [id] = '46000000-0000-0000-0000-000000000005')
    INSERT [vol].[availability] ([id], [volunteer_id], [starts_at], [ends_at], [created_at])
    VALUES ('46000000-0000-0000-0000-000000000005', '45000000-0000-0000-0000-000000000005', DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 30, SYSUTCDATETIME()), SYSUTCDATETIME());
GO
