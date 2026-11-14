IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Action] nvarchar(100) NOT NULL,
    [EntityName] nvarchar(100) NOT NULL,
    [EntityId] nvarchar(100) NULL,
    [Details] nvarchar(2000) NULL,
    [IpAddress] nvarchar(45) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [Organisations] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [OrganisationType] nvarchar(100) NOT NULL,
    [RegistrationNumber] nvarchar(100) NULL,
    [ContactEmail] nvarchar(500) NULL,
    [ContactPhone] nvarchar(20) NULL,
    [Address] nvarchar(500) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Organisations] PRIMARY KEY ([Id])
);

CREATE TABLE [OutboxMessages] (
    [Id] uniqueidentifier NOT NULL,
    [Type] nvarchar(200) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Status] nvarchar(30) NOT NULL,
    [RetryCount] int NOT NULL,
    [ProcessedAt] datetime2 NULL,
    [NextRetryAt] datetime2 NULL,
    [ErrorMessage] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
);

CREATE TABLE [Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [OrganisationId] uniqueidentifier NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [EmailEncrypted] nvarchar(1000) NULL,
    [EmailHash] nvarchar(64) NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [IsEmailVerified] bit NOT NULL,
    [Status] nvarchar(30) NOT NULL,
    [LastLoginAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Organisations_OrganisationId] FOREIGN KEY ([OrganisationId]) REFERENCES [Organisations] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Message] nvarchar(2000) NOT NULL,
    [Channel] nvarchar(50) NOT NULL,
    [Status] nvarchar(30) NOT NULL,
    [SentAt] datetime2 NULL,
    [ReadAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RefreshTokens] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TokenHash] nvarchar(64) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [RevokedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [AssignedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);

CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);

CREATE INDEX [IX_Notifications_Status] ON [Notifications] ([Status]);

CREATE INDEX [IX_Notifications_UserId_CreatedAt] ON [Notifications] ([UserId], [CreatedAt]);

CREATE INDEX [IX_Organisations_Name] ON [Organisations] ([Name]);

CREATE INDEX [IX_OutboxMessages_CreatedAt] ON [OutboxMessages] ([CreatedAt]);

CREATE INDEX [IX_OutboxMessages_Status_NextRetryAt] ON [OutboxMessages] ([Status], [NextRetryAt]);

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);

CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);

CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

CREATE UNIQUE INDEX [IX_Users_EmailHash] ON [Users] ([EmailHash]) WHERE [EmailHash] IS NOT NULL;

CREATE INDEX [IX_Users_OrganisationId] ON [Users] ([OrganisationId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261113201651_InitialCreate', N'10.0.11');

COMMIT;
GO

BEGIN TRANSACTION;
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] ON;
INSERT INTO [Roles] ([Id], [CreatedAt], [Description], [Name], [UpdatedAt])
VALUES ('10000000-0000-0000-0000-000000000001', '2026-11-14T00:00:00.0000000Z', N'Affected person or citizen', N'Citizen', NULL),
('10000000-0000-0000-0000-000000000002', '2026-11-14T00:00:00.0000000Z', N'Emergency coordination and crisis approvals', N'EmergencyCoordinator', NULL),
('10000000-0000-0000-0000-000000000003', '2026-11-14T00:00:00.0000000Z', N'Incident and case management', N'CaseManager', NULL),
('10000000-0000-0000-0000-000000000004', '2026-11-14T00:00:00.0000000Z', N'Resource and logistics management', N'LogisticsManager', NULL),
('10000000-0000-0000-0000-000000000005', '2026-11-14T00:00:00.0000000Z', N'Shelter operations and capacity management', N'ShelterAdministrator', NULL),
('10000000-0000-0000-0000-000000000006', '2026-11-14T00:00:00.0000000Z', N'Volunteer verification and coordination', N'VolunteerCoordinator', NULL),
('10000000-0000-0000-0000-000000000007', '2026-11-14T00:00:00.0000000Z', N'Donor and funding management', N'DonorOfficer', NULL),
('10000000-0000-0000-0000-000000000008', '2026-11-14T00:00:00.0000000Z', N'Partner organisation operations', N'PartnerOrganisationOfficer', NULL),
('10000000-0000-0000-0000-000000000009', '2026-11-14T00:00:00.0000000Z', N'Platform administration', N'SystemAdministrator', NULL),
('10000000-0000-0000-0000-000000000010', '2026-11-14T00:00:00.0000000Z', N'Volunteer task participation', N'Volunteer', NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Description', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] OFF;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261113213143_SeedSystemRoles', N'10.0.11');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Notifications] DROP CONSTRAINT [FK_Notifications_Users_UserId];

DROP INDEX [IX_OutboxMessages_Status_NextRetryAt] ON [OutboxMessages];

CREATE INDEX [IX_OutboxMessages_Status_NextRetryAt_Failed] ON [OutboxMessages] ([Status], [NextRetryAt]) WHERE [Status] = 'Failed';

CREATE INDEX [IX_OutboxMessages_Status_NextRetryAt_Pending] ON [OutboxMessages] ([Status], [NextRetryAt]) WHERE [Status] = 'Pending';

ALTER TABLE [Notifications] ADD CONSTRAINT [FK_notifications_users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261114061728_V4FoundationCorrections', N'10.0.11');

COMMIT;
GO
