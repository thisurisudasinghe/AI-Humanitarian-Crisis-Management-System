-- ============================================================================
-- 99_seed_users.sql - Dev 1 roles, demo organisation, demo users and settings
-- ============================================================================
-- Idempotent development seed. Safe to re-run: existing rows are skipped.
--
-- Password for every demo account: Demo1234!
-- The email hashes and encrypted emails below were produced with the fixed
-- development-only keys in backend/src/Cms.Api/appsettings.Development.json.
-- Do not use those keys, or this seed, outside local development.
--
-- Run after 00_common.sql and 98_foreign_keys.sql.
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
-- Demo users
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000001'
       OR [email_hash] = N'D954B6C7F162810849EB87528E86A11CEABA67F4D6544D8D3D712F65E084263F')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000001',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Admin', N'User', NULL,
        N'lSFnhW9XcmJWGw8iKmPdASRCawpTfIy/xHBjTZNTcxGYtst6WqnQoZV074Hv',
        N'D954B6C7F162810849EB87528E86A11CEABA67F4D6544D8D3D712F65E084263F',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000002'
       OR [email_hash] = N'AD6F75BD2CB90AE3CD93428BECC41FC94F6854DA6E41EF3E21251304211081AB')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000002',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Coordinator', N'User', NULL,
        N'UyJFpiO2BNUFTfjZnBcUpWOvFWf0Dik/s3uR25QprGDZ/kfmmbLiOxu/w5ihbo7p3ZKr',
        N'AD6F75BD2CB90AE3CD93428BECC41FC94F6854DA6E41EF3E21251304211081AB',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000003'
       OR [email_hash] = N'802C0FAD9AE4FCCADF55971F397E42CF0367AC36651F86F5992578DC18260B4E')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000003',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Case', N'Manager', NULL,
        N'Du5UwXK4N3qZrNe/FNkxSsrdoutTQAcUih9gon2jNFyn0Zn6+wIsF9IZGdeqzlAMrALF',
        N'802C0FAD9AE4FCCADF55971F397E42CF0367AC36651F86F5992578DC18260B4E',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000004'
       OR [email_hash] = N'29C6EC25BEF0CBA3B96E2A4F23FAABE50211974D0E388739836E6C4C19816A79')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000004',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Logistics', N'Manager', NULL,
        N'cz0Hem5rxDd+5olEdmx36Di7IxaEzl7atZE13jWUQx7Odn0e35k6uZIDx1Oc4odGLQ==',
        N'29C6EC25BEF0CBA3B96E2A4F23FAABE50211974D0E388739836E6C4C19816A79',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000005'
       OR [email_hash] = N'495D28110644E0444C4A7973EE417CFFCAEF5119BD944DA5E727C760A10F834A')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000005',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Shelter', N'Admin', NULL,
        N'LahSDDGcEph+2VT55Pb3JOx8HHEyTk65yKikUSuHXtZ6OexSPJZ1sLPd5KFZblY=',
        N'495D28110644E0444C4A7973EE417CFFCAEF5119BD944DA5E727C760A10F834A',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000006'
       OR [email_hash] = N'FD8AEDF3EA0C02337206D99366E83D0D67964DBAED4583BE4FA2331399AB8C84')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000006',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Volunteer', N'Coordinator', NULL,
        N'Zw+9xhs/dBBDHVC4UDFtEHrM2QIB4LfW2J7y1KExPT7N+sC90xzInMbBxMbmLsJQ29l+af3RnRrD8H/c',
        N'FD8AEDF3EA0C02337206D99366E83D0D67964DBAED4583BE4FA2331399AB8C84',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000007'
       OR [email_hash] = N'BD1A1587D5D43D51CB7F34D9CDCED0847D63CC38044A7B6A8FBD0908DDD05442')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000007',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Volunteer', N'User', NULL,
        N'M5qYTIDrB9QTymcFlsEwfA3Kio+znXUh5cWNS//+GrmFpm5EP4KVr+/TL8S7mXOl5g==',
        N'BD1A1587D5D43D51CB7F34D9CDCED0847D63CC38044A7B6A8FBD0908DDD05442',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000008'
       OR [email_hash] = N'0403883D1DB5DC5165B67808E04F8ADCD4BE7FA6A2BC080CFCA0CB512583961B')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000008',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Donor', N'Officer', NULL,
        N'J0McAjqkpC88OXs2s4FSSxSN0DLK5ed9jbk/R3fg8Wmhd57Uxu1A9/AR8w4k',
        N'0403883D1DB5DC5165B67808E04F8ADCD4BE7FA6A2BC080CFCA0CB512583961B',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000009'
       OR [email_hash] = N'B2465F3441B03FC8DF0BF83AE62A9EB105DB4936618EEDDD08AD72CA4AFCA290')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000009',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Partner', N'Officer', NULL,
        N'KQO/VODDpr4bgZPrGeTDTLmXcsAW3z45CFtbDuYx/Cnl005m5qWtj3nkrngIkk0=',
        N'B2465F3441B03FC8DF0BF83AE62A9EB105DB4936618EEDDD08AD72CA4AFCA290',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000010'
       OR [email_hash] = N'7F523F20E181EB3880E69AB1729E9EF10D5D06B808C70DA450395C09AB9915DE')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000010',
        NULL,
        N'Citizen', N'User', NULL,
        N'9J92Llc2KxPU43/gCvp3w8H+rj4xqIjEDYY4idzdj3/KQkxfZEvQr7NsXL5VOsc=',
        N'7F523F20E181EB3880E69AB1729E9EF10D5D06B808C70DA450395C09AB9915DE',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000011'
       OR [email_hash] = N'EAE7C6FFA0FC09541EEEB37F972B2639F5743A54CD80172C0BF31EC8DFE5F4D1')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000011',
        (SELECT TOP (1) [id] FROM [common].[organisations] WHERE [name] = N'Lead Agency'),
        N'Multi', N'Role User', NULL,
        N'6ERKeG4+nxfZcjhDJqSU+dBynxN524gm9vRwSg6IfBLMbYU1fgzoiEm/tivo',
        N'EAE7C6FFA0FC09541EEEB37F972B2639F5743A54CD80172C0BF31EC8DFE5F4D1',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Active', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[users]
    WHERE [id] = '0b000000-0000-0000-0000-000000000012'
       OR [email_hash] = N'D16F18F4ACF198ACC83745BC143FDF5C298C8CBDF84CB3C8102475B5AFBA631A')
BEGIN
    INSERT INTO [identity].[users] (
        [id], [organisation_id], [first_name], [last_name], [phone_number],
        [email_encrypted], [email_hash], [password_hash], [is_email_verified],
        [status], [last_login_at], [created_at], [updated_at])
    VALUES (
        '0b000000-0000-0000-0000-000000000012',
        NULL,
        N'Inactive', N'User', NULL,
        N'DVMDsmorUyJr4lu6Rd6L5Nbdf4Zam9RQV4Fzc5X9Cp3Q9BM4RGgt/iK/INqBuDZq',
        N'D16F18F4ACF198ACC83745BC143FDF5C298C8CBDF84CB3C8102475B5AFBA631A',
        N'AQAAAAIAAYagAAAAEFXrqJ4Y9JafelsejibHJ1RZiArMX23oCaUpE7JSBraQ9Q8L9LK7Oqc3YWMdZvKf5Q==',
        1, N'Inactive', NULL, '2026-11-01T00:00:00', NULL);
END;
GO

-- ----------------------------------------------------------------------------
-- Role assignments
-- ----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'D954B6C7F162810849EB87528E86A11CEABA67F4D6544D8D3D712F65E084263F'
      AND r.[name] = N'SystemAdmin')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'D954B6C7F162810849EB87528E86A11CEABA67F4D6544D8D3D712F65E084263F'
      AND r.[name] = N'SystemAdmin';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'AD6F75BD2CB90AE3CD93428BECC41FC94F6854DA6E41EF3E21251304211081AB'
      AND r.[name] = N'EmergencyCoordinator')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'AD6F75BD2CB90AE3CD93428BECC41FC94F6854DA6E41EF3E21251304211081AB'
      AND r.[name] = N'EmergencyCoordinator';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'802C0FAD9AE4FCCADF55971F397E42CF0367AC36651F86F5992578DC18260B4E'
      AND r.[name] = N'CaseManager')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'802C0FAD9AE4FCCADF55971F397E42CF0367AC36651F86F5992578DC18260B4E'
      AND r.[name] = N'CaseManager';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'29C6EC25BEF0CBA3B96E2A4F23FAABE50211974D0E388739836E6C4C19816A79'
      AND r.[name] = N'LogisticsManager')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'29C6EC25BEF0CBA3B96E2A4F23FAABE50211974D0E388739836E6C4C19816A79'
      AND r.[name] = N'LogisticsManager';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'495D28110644E0444C4A7973EE417CFFCAEF5119BD944DA5E727C760A10F834A'
      AND r.[name] = N'ShelterAdmin')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'495D28110644E0444C4A7973EE417CFFCAEF5119BD944DA5E727C760A10F834A'
      AND r.[name] = N'ShelterAdmin';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'FD8AEDF3EA0C02337206D99366E83D0D67964DBAED4583BE4FA2331399AB8C84'
      AND r.[name] = N'VolunteerCoordinator')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'FD8AEDF3EA0C02337206D99366E83D0D67964DBAED4583BE4FA2331399AB8C84'
      AND r.[name] = N'VolunteerCoordinator';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'BD1A1587D5D43D51CB7F34D9CDCED0847D63CC38044A7B6A8FBD0908DDD05442'
      AND r.[name] = N'Volunteer')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'BD1A1587D5D43D51CB7F34D9CDCED0847D63CC38044A7B6A8FBD0908DDD05442'
      AND r.[name] = N'Volunteer';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'0403883D1DB5DC5165B67808E04F8ADCD4BE7FA6A2BC080CFCA0CB512583961B'
      AND r.[name] = N'DonorOfficer')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'0403883D1DB5DC5165B67808E04F8ADCD4BE7FA6A2BC080CFCA0CB512583961B'
      AND r.[name] = N'DonorOfficer';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'B2465F3441B03FC8DF0BF83AE62A9EB105DB4936618EEDDD08AD72CA4AFCA290'
      AND r.[name] = N'PartnerOfficer')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'B2465F3441B03FC8DF0BF83AE62A9EB105DB4936618EEDDD08AD72CA4AFCA290'
      AND r.[name] = N'PartnerOfficer';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'7F523F20E181EB3880E69AB1729E9EF10D5D06B808C70DA450395C09AB9915DE'
      AND r.[name] = N'Citizen')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'7F523F20E181EB3880E69AB1729E9EF10D5D06B808C70DA450395C09AB9915DE'
      AND r.[name] = N'Citizen';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'EAE7C6FFA0FC09541EEEB37F972B2639F5743A54CD80172C0BF31EC8DFE5F4D1'
      AND r.[name] = N'EmergencyCoordinator')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'EAE7C6FFA0FC09541EEEB37F972B2639F5743A54CD80172C0BF31EC8DFE5F4D1'
      AND r.[name] = N'EmergencyCoordinator';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'EAE7C6FFA0FC09541EEEB37F972B2639F5743A54CD80172C0BF31EC8DFE5F4D1'
      AND r.[name] = N'CaseManager')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'EAE7C6FFA0FC09541EEEB37F972B2639F5743A54CD80172C0BF31EC8DFE5F4D1'
      AND r.[name] = N'CaseManager';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM [identity].[user_roles] AS ur
    INNER JOIN [identity].[users] AS u ON u.[id] = ur.[user_id]
    INNER JOIN [identity].[roles] AS r ON r.[id] = ur.[role_id]
    WHERE u.[email_hash] = N'D16F18F4ACF198ACC83745BC143FDF5C298C8CBDF84CB3C8102475B5AFBA631A'
      AND r.[name] = N'Citizen')
BEGIN
    INSERT INTO [identity].[user_roles] ([user_id], [role_id], [assigned_at])
    SELECT u.[id], r.[id], '2026-11-01T00:00:00'
    FROM [identity].[users] AS u
    CROSS JOIN [identity].[roles] AS r
    WHERE u.[email_hash] = N'D16F18F4ACF198ACC83745BC143FDF5C298C8CBDF84CB3C8102475B5AFBA631A'
      AND r.[name] = N'Citizen';
END;
GO

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

