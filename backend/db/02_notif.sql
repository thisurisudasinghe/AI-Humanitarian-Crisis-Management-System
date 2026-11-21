-- ============================================================================
-- 02_notif.sql - Dev 3 notification templates (M9, P2 FR-COM-06)
-- ============================================================================
-- Run after 00_common.sql and before 03_ai.sql / 98_foreign_keys.sql.
-- The team evolved the PDF's notif.NotificationTemplates name to the shared
-- common schema and snake_case convention used by every V4 SQL script.
-- This script is idempotent: it creates missing objects, seeds missing rows,
-- never drops tables and never updates existing template text.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'[common].[notification_templates]', N'U') IS NULL
BEGIN
    CREATE TABLE [common].[notification_templates] (
        [id] uniqueidentifier NOT NULL,
        [template_key] nvarchar(100) NOT NULL,
        [language] nvarchar(10) NOT NULL,
        [subject] nvarchar(200) NOT NULL,
        [body] nvarchar(4000) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_notification_templates] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_notification_templates_key_language'
      AND [object_id] = OBJECT_ID(N'[common].[notification_templates]'))
BEGIN
    CREATE UNIQUE INDEX [UX_notification_templates_key_language]
        ON [common].[notification_templates] ([template_key], [language]);
END;
GO

-- Seed only when the key/language pair is absent, so local edits survive reruns.
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Request.StatusChanged' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Request.StatusChanged', N'en', N'Your request status has changed', N'Request {requestId} is now {status}.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Request.StatusChanged' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Request.StatusChanged', N'es', N'El estado de su solicitud ha cambiado', N'La solicitud {requestId} ahora está {status}.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Request.Assigned' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Request.Assigned', N'en', N'A responder has been assigned', N'Your request {requestId} has been assigned to {assignee}.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Request.Assigned' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Request.Assigned', N'es', N'Se ha asignado un responder', N'Su solicitud {requestId} ha sido asignada a {assignee}.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Shelter.Allocated' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Shelter.Allocated', N'en', N'Shelter allocation confirmed', N'You have been allocated to {shelter}.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Shelter.Allocated' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Shelter.Allocated', N'es', N'Asignación de refugio confirmada', N'Ha sido asignado a {shelter}.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Donation.Received' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Donation.Received', N'en', N'Donation received', N'Thank you. Your donation of {amount} {currency} has been recorded.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Donation.Received' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Donation.Received', N'es', N'Donación recibida', N'Gracias. Su donación de {amount} {currency} ha sido registrada.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Account.Welcome' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Account.Welcome', N'en', N'Welcome to SAFEWAY', N'Hello {name}, your account is ready.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Account.Welcome' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Account.Welcome', N'es', N'Bienvenido a SAFEWAY', N'Hola {name}, su cuenta está lista.', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Emergency.Announcement' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Emergency.Announcement', N'en', N'Emergency announcement', N'{message}', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Emergency.Announcement' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Emergency.Announcement', N'es', N'Anuncio de emergencia', N'{message}', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Announcement.Approved' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Announcement.Approved', N'en', N'Emergency announcement', N'{message}', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Announcement.Approved' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Announcement.Approved', N'es', N'Anuncio de emergencia', N'{message}', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Announcement.Rejected' AND [language] = N'en')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Announcement.Rejected', N'en', N'Announcement update', N'{message}', SYSUTCDATETIME());
GO
IF NOT EXISTS (SELECT 1 FROM [common].[notification_templates] WHERE [template_key] = N'Announcement.Rejected' AND [language] = N'es')
    INSERT INTO [common].[notification_templates] ([id], [template_key], [language], [subject], [body], [created_at])
    VALUES (NEWID(), N'Announcement.Rejected', N'es', N'Actualización del anuncio', N'{message}', SYSUTCDATETIME());
GO
