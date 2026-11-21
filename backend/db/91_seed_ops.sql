-- Member 4 operations demonstration data. Idempotent fixed identifiers.
-- Coordinate the limited inventory quantity/type with Member 2's scenario seed before UAT.
-- Column names follow the snake_case CmsDbContext model.

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM [res].[resource_types] WHERE [id] = '41000000-0000-0000-0000-000000000001')
BEGIN
    INSERT [res].[resource_types] ([id], [name], [unit], [description], [created_at])
    VALUES
        ('41000000-0000-0000-0000-000000000001', N'Emergency food pack', N'packs', N'Deliberately limited demo stock', SYSUTCDATETIME()),
        ('41000000-0000-0000-0000-000000000002', N'Medical kit', N'kits', N'General first aid kit', SYSUTCDATETIME()),
        ('41000000-0000-0000-0000-000000000003', N'Clean water', N'litres', N'Drinking water supply', SYSUTCDATETIME());
END;
GO

IF NOT EXISTS (SELECT 1 FROM [res].[inventories] WHERE [id] = '42000000-0000-0000-0000-000000000001')
BEGIN
    INSERT [res].[inventories] (
        [id], [resource_type_id], [org_id], [quantity], [available], [reserved],
        [allocated], [dispatched], [utilised], [reorder_threshold], [lat], [lng],
        [accessibility_capable], [created_at])
    VALUES
        ('42000000-0000-0000-0000-000000000001', '41000000-0000-0000-0000-000000000001', '43000000-0000-0000-0000-000000000001', 5, 5, 0, 0, 0, 0, 2, 7.2088, 79.8358, 0, SYSUTCDATETIME()),
        ('42000000-0000-0000-0000-000000000002', '41000000-0000-0000-0000-000000000002', '43000000-0000-0000-0000-000000000001', 30, 30, 0, 0, 0, 0, 5, 7.2088, 79.8358, 1, SYSUTCDATETIME()),
        ('42000000-0000-0000-0000-000000000003', '41000000-0000-0000-0000-000000000003', '43000000-0000-0000-0000-000000000001', 100, 100, 0, 0, 0, 0, 20, 7.2088, 79.8358, 1, SYSUTCDATETIME());
END;
GO

IF NOT EXISTS (SELECT 1 FROM [shelter].[shelters] WHERE [id] = '44000000-0000-0000-0000-000000000001')
BEGIN
    INSERT [shelter].[shelters] (
        [id], [name], [capacity], [occupancy], [facilities], [accessibility_features],
        [services], [lat], [lng], [org_id], [approved], [created_at])
    VALUES
        ('44000000-0000-0000-0000-000000000001', N'Negombo Community Hall', 120, 40, N'["Water","Sanitation"]', N'["Wheelchair access"]', N'["Food","First aid"]', 7.2088, 79.8358, '43000000-0000-0000-0000-000000000001', 1, SYSUTCDATETIME()),
        ('44000000-0000-0000-0000-000000000002', N'Ja-Ela Relief Centre', 80, 22, N'["Water","Power"]', N'["Accessible toilets"]', N'["Food","Shelter"]', 7.0744, 79.8919, '43000000-0000-0000-0000-000000000001', 1, SYSUTCDATETIME()),
        ('44000000-0000-0000-0000-000000000003', N'Wattala Temporary Shelter', 60, 60, N'["Water"]', N'[]', N'["Shelter"]', 6.9890, 79.8910, '43000000-0000-0000-0000-000000000001', 1, SYSUTCDATETIME());
END;
GO

IF NOT EXISTS (SELECT 1 FROM [vol].[volunteers] WHERE [id] = '45000000-0000-0000-0000-000000000001')
BEGIN
    INSERT [vol].[volunteers] (
        [id], [user_id], [skills_json], [languages_json], [verification_status],
        [active], [lat], [lng], [created_at])
    VALUES
        ('45000000-0000-0000-0000-000000000001', '45100000-0000-0000-0000-000000000001', N'["First aid","Driving"]', N'["English","Sinhala"]', N'Verified', 1, 7.2088, 79.8358, SYSUTCDATETIME()),
        ('45000000-0000-0000-0000-000000000002', '45100000-0000-0000-0000-000000000002', N'["Food distribution"]', N'["Sinhala"]', N'Verified', 1, 7.0744, 79.8919, SYSUTCDATETIME()),
        ('45000000-0000-0000-0000-000000000003', '45100000-0000-0000-0000-000000000003', N'["Medical support"]', N'["English"]', N'Verified', 1, 6.9890, 79.8910, SYSUTCDATETIME()),
        ('45000000-0000-0000-0000-000000000004', '45100000-0000-0000-0000-000000000004', N'["Logistics"]', N'["Sinhala","Tamil"]', N'Pending', 1, 7.2088, 79.8358, SYSUTCDATETIME()),
        ('45000000-0000-0000-0000-000000000005', '45100000-0000-0000-0000-000000000005', N'["Shelter support"]', N'["Tamil"]', N'Verified', 1, 7.0744, 79.8919, SYSUTCDATETIME());
END;
GO
