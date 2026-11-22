-- ============================================================================
-- 90_seed_scenario.sql - DEV2 (D2-12) Cases workflow demonstration data
-- ============================================================================
-- Owner: Developer 2 (M2 Crisis & Case Management)
-- Run order: after 00_common.sql, 01_cases.sql, 98_foreign_keys.sql and
--            99_seed_users.sql, and after the API has created the Development
--            demo users (DevelopmentDemoSeeder). Run before 91_seed_ops.sql.
--
-- This script inserts ONLY into cases.* tables. It never inserts, updates or
-- deletes rows in identity.*, common.*, ai.*, res.*, shelter.*, vol.*, geo.*
-- or don.*. Required shared rows are validated and reported as dependencies.
--
-- The scenario uses fixed GUIDs and skips any row whose primary key already
-- exists, so it is safe to re-run. Sensitive household/accessibility values are
-- deliberately left NULL; create those through the API so the existing field
-- encryption path is exercised.
--
-- Cross-module references used (created by Member 1, not by this script):
--   identity.users 0b000000-0000-0000-0000-000000000010 (Citizen)
--   identity.users 0b000000-0000-0000-0000-000000000002 (EmergencyCoordinator)
--   identity.users 0b000000-0000-0000-0000-000000000003 (CaseManager)
--   common.organisations 0f000000-0000-0000-0000-000000000001 (Lead Agency)
--
-- RESERVED FOR MEMBER 4 - DO NOT INSERT IN THIS SCRIPT (D2-12 shortage demo):
--   res.Inventories.Id         = 42000000-0000-0000-0000-000000000001
--   res.ResourceTypes.Id       = 41000000-0000-0000-0000-000000000001
--   Resource type / quantity   = 'Emergency food pack' / 5 packs
--   Owner script               = backend/db/91_seed_ops.sql (M4 owns that row)
-- Requests R6, R7 and R8 below are the three urgent Food requests competing for
-- that single limited-resource bucket. No FK is created from cases.requests to
-- res.Inventories, so this script may run before 91_seed_ops.sql.
--
-- Expected fixture coverage:
--   1 crisis, 3 areas, 10 requests spanning early and late status values,
--   one back-dated Submitted request for /api/v1/requests/overdue,
--   one Critical request for the CloseCase decision gate, and the three-request
--   limited-resource shortage group described above.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @CitizenUserId UNIQUEIDENTIFIER = '0b000000-0000-0000-0000-000000000010';
DECLARE @CoordinatorUserId UNIQUEIDENTIFIER = '0b000000-0000-0000-0000-000000000002';
DECLARE @CaseManagerUserId UNIQUEIDENTIFIER = '0b000000-0000-0000-0000-000000000003';
DECLARE @LeadAgencyOrgId UNIQUEIDENTIFIER = '0f000000-0000-0000-0000-000000000001';

BEGIN TRY
    IF OBJECT_ID(N'[cases].[crises]', N'U') IS NULL
    BEGIN
        THROW 50001, N'Missing cases schema: run backend/db/01_cases.sql first.', 1;
    END;

    IF OBJECT_ID(N'[identity].[users]', N'U') IS NULL
    BEGIN
        THROW 50002, N'Missing identity.users: run backend/db/00_common.sql first.', 1;
    END;

    IF OBJECT_ID(N'[common].[organisations]', N'U') IS NULL
    BEGIN
        THROW 50003, N'Missing common.organisations: run backend/db/00_common.sql and 99_seed_users.sql first.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM [identity].[users] WHERE [id] = @CitizenUserId)
       OR NOT EXISTS (SELECT 1 FROM [identity].[users] WHERE [id] = @CoordinatorUserId)
       OR NOT EXISTS (SELECT 1 FROM [identity].[users] WHERE [id] = @CaseManagerUserId)
    BEGIN
        THROW 50004, N'Missing one or more demo users. Run backend/db/99_seed_users.sql and start the API once so DevelopmentDemoSeeder creates them.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM [common].[organisations] WHERE [id] = @LeadAgencyOrgId)
    BEGIN
        THROW 50005, N'Missing Lead Agency organisation. Run backend/db/99_seed_users.sql first (Member 1 owns that file).', 1;
    END;

    BEGIN TRAN;

    -- ------------------------------------------------------------------------
    -- Crisis
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[crises] WHERE [id] = '1a000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[crises]
            ([id], [name], [type], [description], [status], [started_at], [ended_at], [created_at], [updated_at])
        VALUES
            ('1a000000-0000-0000-0000-000000000001',
             N'North Coastal Flood Response', N'Flood',
             N'Demo crisis for the DEV2 request-to-closure workflow.',
             N'Active', '2026-11-17T06:00:00', NULL, '2026-11-17T06:00:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Areas
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[areas] WHERE [id] = '1b000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[areas]
            ([id], [crisis_id], [name], [description], [lat], [lng], [radius_km], [created_at], [updated_at])
        VALUES
            ('1b000000-0000-0000-0000-000000000001',
             '1a000000-0000-0000-0000-000000000001',
             N'Harbour Ward', N'Low-lying coastal neighbourhood.',
             7.2900000, 80.6400000, 5.00, '2026-11-17T06:10:00', NULL);
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[areas] WHERE [id] = '1b000000-0000-0000-0000-000000000002')
    BEGIN
        INSERT INTO [cases].[areas]
            ([id], [crisis_id], [name], [description], [lat], [lng], [radius_km], [created_at], [updated_at])
        VALUES
            ('1b000000-0000-0000-0000-000000000002',
             '1a000000-0000-0000-0000-000000000001',
             N'Riverside Ward', N'Riverbank settlement with limited access.',
             7.3100000, 80.6800000, 4.00, '2026-11-17T06:12:00', NULL);
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[areas] WHERE [id] = '1b000000-0000-0000-0000-000000000003')
    BEGIN
        INSERT INTO [cases].[areas]
            ([id], [crisis_id], [name], [description], [lat], [lng], [radius_km], [created_at], [updated_at])
        VALUES
            ('1b000000-0000-0000-0000-000000000003',
             '1a000000-0000-0000-0000-000000000001',
             N'Market Ward', N'Informal market district with high population density.',
             7.3000000, 80.6600000, 3.50, '2026-11-17T06:15:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Household profile (no plaintext sensitive values seeded)
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[households] WHERE [id] = '1c000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[households]
            ([id], [user_id], [size], [accessibility_needs], [contact_phone], [created_at], [updated_at])
        VALUES
            ('1c000000-0000-0000-0000-000000000001',
             @CitizenUserId, 4, NULL, NULL, '2026-11-17T07:00:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Requests (10 fixed-GUID fixtures)
    -- R1  demonstrates a complete High-urgency lifecycle through Closed.
    -- R2  demonstrates a Critical request that must NOT be closed without an
    --     approved CloseCase decision from Member 3.
    -- R3/R4 demonstrate merge; R5 is a fresh Submitted queue item.
    -- R6/R7/R8 are the three urgent Food requests competing for the single
    --     reserved 'Emergency food pack' bucket owned by Member 4.
    -- R9  demonstrates Allocated -> InProgress dispatch handoff.
    -- R10 is deliberately back-dated so /api/v1/requests/overdue returns it.
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000001',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000001',
             N'Food', N'High',
             N'Family of four needs food parcels after floodwater entered the home.',
             7.2900000, 80.6400000, 4, NULL,
             N'Closed', @CoordinatorUserId, NULL,
             '2026-11-17T07:15:00', '2026-11-17T07:45:00', '2026-11-17T08:10:00',
             '2026-11-18T16:30:00', '2026-11-17T07:15:00', '2026-11-18T16:30:00');
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000002')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000002',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000002',
             N'Medicine', N'Critical',
             N'Urgent prescription refill required; pharmacy access is blocked.',
             7.3100000, 80.6800000, 2, NULL,
             N'Dispatched', @CaseManagerUserId, NULL,
             '2026-11-20T09:00:00', '2026-11-20T09:20:00', '2026-11-20T09:35:00',
             NULL, '2026-11-20T09:00:00', '2026-11-20T14:10:00');
    END;

    -- Insert the merge target before the source because cases.requests has a
    -- self-referencing FK on merged_into_request_id.
    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000004')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000004',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000001',
             N'Shelter', N'Medium',
             N'Primary shelter assistance request for the same household.',
             7.2905000, 80.6405000, 4, NULL,
             N'Verified', @CaseManagerUserId, NULL,
             '2026-11-19T10:50:00', '2026-11-19T11:40:00', NULL,
             NULL, '2026-11-19T10:50:00', '2026-11-19T11:40:00');
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000003')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000003',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000001',
             N'Shelter', N'Medium',
             N'Duplicate shelter report submitted from a second device.',
             7.2910000, 80.6410000, 4, NULL,
             N'Merged', @CaseManagerUserId, '1d000000-0000-0000-0000-000000000004',
             '2026-11-19T11:00:00', '2026-11-19T11:30:00', NULL,
             NULL, '2026-11-19T11:00:00', '2026-11-19T11:45:00');
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000005')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000005',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000002',
             N'Transport', N'Low',
             N'Transport needed to reach a temporary distribution point.',
             7.3120000, 80.6810000, 3, NULL,
             N'Submitted', NULL, NULL,
             '2026-11-21T05:00:00', NULL, NULL,
             NULL, '2026-11-21T05:00:00', NULL);
    END;

    -- R6-R8: limited-resource shortage group (3 urgent Food requests).
    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000006')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000006',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000003',
             N'Food', N'Critical',
             N'Critical: family of five without food after the market-road washout; needs an Emergency food pack.',
             7.3005000, 80.6605000, 5, NULL,
             N'Triaged', @CaseManagerUserId, NULL,
             '2026-11-21T08:30:00', '2026-11-21T08:50:00', '2026-11-21T09:10:00',
             NULL, '2026-11-21T08:30:00', '2026-11-21T09:10:00');
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000007')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000007',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000003',
             N'Food', N'High',
             N'High: displaced vendor household requests an Emergency food pack.',
             7.3010000, 80.6610000, 3, NULL,
             N'Verified', @CoordinatorUserId, NULL,
             '2026-11-21T08:35:00', '2026-11-21T09:00:00', NULL,
             NULL, '2026-11-21T08:35:00', '2026-11-21T09:00:00');
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000008')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000008',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000003',
             N'Food', N'High',
             N'High: two families sharing a shelter need an Emergency food pack.',
             7.3020000, 80.6620000, 6, NULL,
             N'Submitted', NULL, NULL,
             '2026-11-21T08:40:00', NULL, NULL,
             NULL, '2026-11-21T08:40:00', NULL);
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000009')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000009',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000002',
             N'Other', N'Medium',
             N'Clean water containers needed at the riverside collection point.',
             7.3090000, 80.6790000, 4, NULL,
             N'InProgress', @CaseManagerUserId, NULL,
             '2026-11-20T12:00:00', '2026-11-20T12:30:00', '2026-11-20T13:00:00',
             NULL, '2026-11-20T12:00:00', '2026-11-21T07:30:00');
    END;

    -- R10: back-dated so it is always overdue under the default 24-hour SLA.
    IF NOT EXISTS (SELECT 1 FROM [cases].[requests] WHERE [id] = '1d000000-0000-0000-0000-000000000010')
    BEGIN
        INSERT INTO [cases].[requests]
            ([id], [requester_user_id], [crisis_id], [area_id], [category], [urgency],
             [description], [lat], [lng], [household_size], [accessibility_needs],
             [status], [assigned_to_user_id], [merged_into_request_id],
             [submitted_at], [verified_at], [triaged_at], [closed_at], [created_at], [updated_at])
        VALUES
            ('1d000000-0000-0000-0000-000000000010',
             @CitizenUserId,
             '1a000000-0000-0000-0000-000000000001',
             '1b000000-0000-0000-0000-000000000001',
             N'Transport', N'Low',
             N'Elderly resident still waiting for transport to the registration desk.',
             7.2890000, 80.6390000, 2, NULL,
             N'Submitted', NULL, NULL,
             '2026-11-14T06:00:00', NULL, NULL,
             NULL, '2026-11-14T06:00:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Request status history (timeline)
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000001')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000001', '1d000000-0000-0000-0000-000000000001', N'Submitted', N'Submitted', @CitizenUserId, N'Request submitted', '2026-11-17T07:15:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000002')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000002', '1d000000-0000-0000-0000-000000000001', N'Submitted', N'Verified', @CoordinatorUserId, N'Verified against call centre record', '2026-11-17T07:45:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000003')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000003', '1d000000-0000-0000-0000-000000000001', N'Verified', N'Triaged', @CaseManagerUserId, N'Food priority set to High', '2026-11-17T08:10:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000004')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000004', '1d000000-0000-0000-0000-000000000001', N'Triaged', N'Recommended', @CaseManagerUserId, N'Awaiting AI recommendation review', '2026-11-17T08:20:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000005')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000005', '1d000000-0000-0000-0000-000000000001', N'Recommended', N'Approved', @CoordinatorUserId, N'Human approval recorded', '2026-11-17T09:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000006')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000006', '1d000000-0000-0000-0000-000000000001', N'Approved', N'Allocated', @CoordinatorUserId, N'Food parcels allocated', '2026-11-17T09:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000007')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000007', '1d000000-0000-0000-0000-000000000001', N'Allocated', N'Dispatched', @CoordinatorUserId, N'Dispatch handed to logistics', '2026-11-17T10:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000008')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000008', '1d000000-0000-0000-0000-000000000001', N'Dispatched', N'Fulfilled', @CoordinatorUserId, N'Delivery confirmed', '2026-11-18T15:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000009')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000009', '1d000000-0000-0000-0000-000000000001', N'Fulfilled', N'Closed', @CoordinatorUserId, N'Case closed after confirmation', '2026-11-18T16:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000010')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000010', '1d000000-0000-0000-0000-000000000002', N'Submitted', N'Submitted', @CitizenUserId, N'Request submitted', '2026-11-20T09:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000011')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000011', '1d000000-0000-0000-0000-000000000002', N'Submitted', N'Verified', @CoordinatorUserId, N'Urgent medical need verified', '2026-11-20T09:20:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000012')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000012', '1d000000-0000-0000-0000-000000000002', N'Verified', N'Triaged', @CaseManagerUserId, N'Critical medical priority', '2026-11-20T09:35:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000013')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000013', '1d000000-0000-0000-0000-000000000002', N'Triaged', N'Recommended', @CaseManagerUserId, N'M3 recommendation review requested', '2026-11-20T09:40:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000014')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000014', '1d000000-0000-0000-0000-000000000002', N'Recommended', N'Approved', @CoordinatorUserId, N'Human approval recorded', '2026-11-20T10:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000015')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000015', '1d000000-0000-0000-0000-000000000002', N'Approved', N'Allocated', @CoordinatorUserId, N'Medicine pack allocated', '2026-11-20T10:15:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000016')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000016', '1d000000-0000-0000-0000-000000000002', N'Allocated', N'Dispatched', @CoordinatorUserId, N'Dispatched with volunteer courier', '2026-11-20T14:10:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000017')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000017', '1d000000-0000-0000-0000-000000000003', N'Submitted', N'Submitted', @CitizenUserId, N'Request submitted', '2026-11-19T11:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000018')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000018', '1d000000-0000-0000-0000-000000000003', N'Submitted', N'Verified', @CaseManagerUserId, N'Duplicate checked', '2026-11-19T11:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000019')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000019', '1d000000-0000-0000-0000-000000000003', N'Verified', N'Merged', @CaseManagerUserId, N'Merged into primary shelter request 1d000000-0000-0000-0000-000000000004', '2026-11-19T11:45:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000020')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000020', '1d000000-0000-0000-0000-000000000004', N'Submitted', N'Submitted', @CitizenUserId, N'Request submitted', '2026-11-19T10:50:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000021')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000021', '1d000000-0000-0000-0000-000000000004', N'Submitted', N'Verified', @CaseManagerUserId, N'Primary shelter request verified', '2026-11-19T11:40:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000022')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000022', '1d000000-0000-0000-0000-000000000005', N'Submitted', N'Submitted', @CitizenUserId, N'Request submitted', '2026-11-21T05:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000023')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000023', '1d000000-0000-0000-0000-000000000006', N'Submitted', N'Submitted', @CitizenUserId, N'Critical food request submitted', '2026-11-21T08:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000024')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000024', '1d000000-0000-0000-0000-000000000006', N'Submitted', N'Verified', @CoordinatorUserId, N'Critical need verified', '2026-11-21T08:50:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000025')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000025', '1d000000-0000-0000-0000-000000000006', N'Verified', N'Triaged', @CaseManagerUserId, N'Critical food priority set', '2026-11-21T09:10:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000026')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000026', '1d000000-0000-0000-0000-000000000007', N'Submitted', N'Submitted', @CitizenUserId, N'High food request submitted', '2026-11-21T08:35:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000027')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000027', '1d000000-0000-0000-0000-000000000007', N'Submitted', N'Verified', @CoordinatorUserId, N'Vendor household verified', '2026-11-21T09:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000028')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000028', '1d000000-0000-0000-0000-000000000008', N'Submitted', N'Submitted', @CitizenUserId, N'High food request submitted', '2026-11-21T08:40:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000029')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000029', '1d000000-0000-0000-0000-000000000009', N'Submitted', N'Submitted', @CitizenUserId, N'Water container request submitted', '2026-11-20T12:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000030')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000030', '1d000000-0000-0000-0000-000000000009', N'Submitted', N'Verified', @CoordinatorUserId, N'Collection point need verified', '2026-11-20T12:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000031')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000031', '1d000000-0000-0000-0000-000000000009', N'Verified', N'Triaged', @CaseManagerUserId, N'Medium priority set', '2026-11-20T13:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000032')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000032', '1d000000-0000-0000-0000-000000000009', N'Triaged', N'Recommended', @CaseManagerUserId, N'Awaiting allocation review', '2026-11-20T13:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000033')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000033', '1d000000-0000-0000-0000-000000000009', N'Recommended', N'Approved', @CoordinatorUserId, N'Human approval recorded', '2026-11-20T14:00:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000034')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000034', '1d000000-0000-0000-0000-000000000009', N'Approved', N'Allocated', @CoordinatorUserId, N'Water containers allocated', '2026-11-20T14:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000035')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000035', '1d000000-0000-0000-0000-000000000009', N'Allocated', N'InProgress', @CaseManagerUserId, N'Dispatch in progress', '2026-11-21T07:30:00', NULL);

    IF NOT EXISTS (SELECT 1 FROM [cases].[request_status_history] WHERE [id] = '1e000000-0000-0000-0000-000000000036')
        INSERT INTO [cases].[request_status_history] ([id], [request_id], [from_status], [to_status], [changed_by_user_id], [reason], [created_at], [updated_at])
        VALUES ('1e000000-0000-0000-0000-000000000036', '1d000000-0000-0000-0000-000000000010', N'Submitted', N'Submitted', @CitizenUserId, N'Back-dated overdue request submitted', '2026-11-14T06:00:00', NULL);

    -- ------------------------------------------------------------------------
    -- Request links (merge)
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[request_links] WHERE [id] = '1f000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[request_links]
            ([id], [request_id], [linked_request_id], [link_type], [created_at], [updated_at])
        VALUES
            ('1f000000-0000-0000-0000-000000000001',
             '1d000000-0000-0000-0000-000000000003',
             '1d000000-0000-0000-0000-000000000004',
             N'Merged', '2026-11-19T11:45:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Attachment metadata only. No physical files are created by this script.
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[attachments] WHERE [id] = '20000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[attachments]
            ([id], [request_id], [stored_file_name], [original_file_name], [content_type],
             [size_bytes], [uploaded_by_user_id], [created_at], [updated_at])
        VALUES
            ('20000000-0000-0000-0000-000000000001',
             '1d000000-0000-0000-0000-000000000001',
             N'2a000000-0000-0000-0000-000000000001.png',
             N'food-needs.png', N'image/png',
             20480, @CitizenUserId, '2026-11-17T07:20:00', NULL);
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[attachments] WHERE [id] = '20000000-0000-0000-0000-000000000002')
    BEGIN
        INSERT INTO [cases].[attachments]
            ([id], [request_id], [stored_file_name], [original_file_name], [content_type],
             [size_bytes], [uploaded_by_user_id], [created_at], [updated_at])
        VALUES
            ('20000000-0000-0000-0000-000000000002',
             '1d000000-0000-0000-0000-000000000002',
             N'2a000000-0000-0000-0000-000000000002.pdf',
             N'prescription.pdf', N'application/pdf',
             65536, @CitizenUserId, '2026-11-20T09:05:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Alerts (one published, one draft for visibility checks)
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[alerts] WHERE [id] = '21000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[alerts]
            ([id], [crisis_id], [title], [body], [status], [published_at], [created_at], [updated_at])
        VALUES
            ('21000000-0000-0000-0000-000000000001',
             '1a000000-0000-0000-0000-000000000001',
             N'Boil-water notice for Harbour Ward',
             N'Boil drinking water for at least one minute until the supply is retested.',
             N'Published', '2026-11-20T12:00:00', '2026-11-20T11:55:00', NULL);
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[alerts] WHERE [id] = '21000000-0000-0000-0000-000000000002')
    BEGIN
        INSERT INTO [cases].[alerts]
            ([id], [crisis_id], [title], [body], [status], [published_at], [created_at], [updated_at])
        VALUES
            ('21000000-0000-0000-0000-000000000002',
             '1a000000-0000-0000-0000-000000000001',
             N'Draft shelter capacity update',
             N'Internal draft: capacity figures are awaiting coordinator approval.',
             N'Draft', NULL, '2026-11-21T08:00:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Service centres
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[service_centres] WHERE [id] = '22000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[service_centres]
            ([id], [crisis_id], [name], [address], [lat], [lng], [services],
             [opening_hours], [is_active], [created_at], [updated_at])
        VALUES
            ('22000000-0000-0000-0000-000000000001',
             '1a000000-0000-0000-0000-000000000001',
             N'Harbour Ward Relief Centre',
             N'12 Harbour Road, Colombo',
             7.2905000, 80.6405000,
             N'Food parcels; drinking water; wheelchair-accessible intake',
             N'07:00-19:00 daily', 1, '2026-11-17T08:00:00', NULL);
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[service_centres] WHERE [id] = '22000000-0000-0000-0000-000000000002')
    BEGIN
        INSERT INTO [cases].[service_centres]
            ([id], [crisis_id], [name], [address], [lat], [lng], [services],
             [opening_hours], [is_active], [created_at], [updated_at])
        VALUES
            ('22000000-0000-0000-0000-000000000002',
             NULL,
             N'Provincial Coordination Desk',
             N'1 Secretariat Lane, Colombo',
             7.3200000, 80.7000000,
             N'Case registration; referral to partner agencies',
             N'08:00-17:00 weekdays', 1, '2026-11-17T08:05:00', NULL);
    END;

    IF NOT EXISTS (SELECT 1 FROM [cases].[service_centres] WHERE [id] = '22000000-0000-0000-0000-000000000003')
    BEGIN
        INSERT INTO [cases].[service_centres]
            ([id], [crisis_id], [name], [address], [lat], [lng], [services],
             [opening_hours], [is_active], [created_at], [updated_at])
        VALUES
            ('22000000-0000-0000-0000-000000000003',
             '1a000000-0000-0000-0000-000000000001',
             N'Temporary Clinic (closed)',
             N'45 River Road, Colombo',
             7.3110000, 80.6790000,
             N'Basic first aid',
             NULL, 0, '2026-11-18T09:00:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Feedback (only valid for Fulfilled or Closed requests)
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[feedback] WHERE [id] = '23000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[feedback]
            ([id], [request_id], [submitted_by_user_id], [comment], [created_at], [updated_at])
        VALUES
            ('23000000-0000-0000-0000-000000000001',
             '1d000000-0000-0000-0000-000000000001',
             @CitizenUserId,
             N'Food parcel received; the volunteer was helpful.',
             '2026-11-18T17:00:00', NULL);
    END;

    -- ------------------------------------------------------------------------
    -- Partner share (organisation row is Member 1 owned and is not modified)
    -- ------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [cases].[partner_shares] WHERE [id] = '24000000-0000-0000-0000-000000000001')
    BEGIN
        INSERT INTO [cases].[partner_shares]
            ([id], [request_id], [organisation_id], [shared_by_user_id],
             [shared_at], [revoked_at], [created_at], [updated_at])
        VALUES
            ('24000000-0000-0000-0000-000000000001',
             '1d000000-0000-0000-0000-000000000002',
             @LeadAgencyOrgId,
             @CoordinatorUserId,
             '2026-11-20T14:30:00', NULL, '2026-11-20T14:30:00', NULL);
    END;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;
    THROW;
END CATCH;
