-- ============================================================================
-- 99_seed_users.sql - Dev 1 roles, demo organisation, demo users and settings
-- ============================================================================
-- Idempotent development seed. Safe to re-run: existing rows are skipped.
--
-- This script creates the system roles, the demo organisation and the default
-- system settings only. The demo user accounts (password Demo1234!) are
-- created/updated at API startup by DevelopmentDemoSeeder using the current
-- runtime keys, so no key-derived ciphertext or hashes are stored here.
--
-- Run after 00_common.sql and 98_foreign_keys.sql, then start the API once.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ----------------------------------------------------------------------------
-- Roles (exact RoleNames strings)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000001'
       OR [name] = N'Citizen')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000001', N'Citizen', N'Affected person or citizen', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000002'
       OR [name] = N'EmergencyCoordinator')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000002', N'EmergencyCoordinator', N'Emergency coordination and crisis approvals', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000003'
       OR [name] = N'CaseManager')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000003', N'CaseManager', N'Incident and case management', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000004'
       OR [name] = N'LogisticsManager')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000004', N'LogisticsManager', N'Resource and logistics management', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000005'
       OR [name] = N'ShelterAdmin')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000005', N'ShelterAdmin', N'Shelter operations and capacity management', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000006'
       OR [name] = N'VolunteerCoordinator')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000006', N'VolunteerCoordinator', N'Volunteer verification and coordination', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000007'
       OR [name] = N'Volunteer')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000007', N'Volunteer', N'Volunteer task participation', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000008'
       OR [name] = N'DonorOfficer')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000008', N'DonorOfficer', N'Donor and funding management', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000009'
       OR [name] = N'PartnerOfficer')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000009', N'PartnerOfficer', N'Partner organisation operations', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000010'
       OR [name] = N'SystemAdmin')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000010', N'SystemAdmin', N'Platform administration', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[roles]
    WHERE [id] = '0a000000-0000-0000-0000-000000000011'
       OR [name] = N'Donor')
BEGIN
    INSERT INTO [identity].[roles] ([id], [name], [description], [created_at], [updated_at])
    VALUES ('0a000000-0000-0000-0000-000000000011', N'Donor', N'Public donor participant', '2026-11-01T00:00:00', NULL);
END;
GO

-- ----------------------------------------------------------------------------
-- Demo organisation
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM [common].[organisations]
    WHERE [id] = '0f000000-0000-0000-0000-000000000001'
       OR [name] = N'Lead Agency')
BEGIN
    INSERT INTO [common].[organisations] (
        [id], [name], [organisation_type], [registration_number],
        [contact_email], [contact_phone], [address], [is_active],
        [created_at], [updated_at])
    VALUES (
        '0f000000-0000-0000-0000-000000000001',
        N'Lead Agency', N'Government', N'DEMO-ORG-001',
        NULL, NULL, N'Demo Headquarters', 1,
        '2026-11-01T00:00:00', NULL);
END;
GO

-- ----------------------------------------------------------------------------

-- ----------------------------------------------------------------------------
-- Demo users
-- ----------------------------------------------------------------------------
-- Demo users are created/updated at API startup by DevelopmentDemoSeeder using
-- the current EmailProtection and password hashing keys. They are deliberately
-- not stored here so this script never contains key-derived ciphertext or hashes.

-- ----------------------------------------------------------------------------
-- Default system settings
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM [common].[system_settings]
    WHERE [id] = '0c000000-0000-0000-0000-000000000001'
       OR [key] = N'SlaHours')
BEGIN
    INSERT INTO [common].[system_settings] ([id], [key], [value], [description], [created_at], [updated_at])
    VALUES ('0c000000-0000-0000-0000-000000000001', N'SlaHours', N'24',
            N'Default service-level target in hours.', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [common].[system_settings]
    WHERE [id] = '0c000000-0000-0000-0000-000000000002'
       OR [key] = N'ReorderThreshold')
BEGIN
    INSERT INTO [common].[system_settings] ([id], [key], [value], [description], [created_at], [updated_at])
    VALUES ('0c000000-0000-0000-0000-000000000002', N'ReorderThreshold', N'10',
            N'Default stock level that triggers a reorder recommendation.', '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [common].[system_settings]
    WHERE [id] = '0c000000-0000-0000-0000-000000000003'
       OR [key] = N'SuspiciousDonationAmount')
BEGIN
    INSERT INTO [common].[system_settings] ([id], [key], [value], [description], [created_at], [updated_at])
    VALUES ('0c000000-0000-0000-0000-000000000003', N'SuspiciousDonationAmount', N'10000',
            N'Donation value above which a human review flag is raised.', '2026-11-01T00:00:00', NULL);
END;
GO
