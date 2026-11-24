-- ============================================================================
-- 00_identity_email_verification.sql - Safeway email verification tokens
-- ============================================================================
-- Run this script after 00_common.sql and before 98_foreign_keys.sql.
-- It is idempotent: it creates only missing objects and never drops data.
--
-- Security notes:
--   * token_hash stores the uppercase SHA-256 hex of a 32-byte random token.
--     The raw token is never written to the database.
--   * The filtered unique index allows many historical tokens per user but
--     only one ACTIVE token at a time.
--
-- Existing databases with is_email_verified already present need no user-table
-- changes. This script only adds the token table.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[identity].[email_verification_tokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[email_verification_tokens]
    (
        [id] uniqueidentifier NOT NULL
            CONSTRAINT [PK_email_verification_tokens] PRIMARY KEY,
        [user_id] uniqueidentifier NOT NULL,
        [token_hash] nvarchar(64) NOT NULL,
        [created_at] datetime2 NOT NULL
            CONSTRAINT [DF_email_verification_tokens_created_at] DEFAULT (SYSUTCDATETIME()),
        [updated_at] datetime2 NULL,
        [expires_at] datetime2 NOT NULL,
        [consumed_at] datetime2 NULL,
        [revoked_at] datetime2 NULL
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM [sys].[indexes]
    WHERE [name] = N'UX_email_verification_tokens_token_hash'
      AND [object_id] = OBJECT_ID(N'[identity].[email_verification_tokens]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_email_verification_tokens_token_hash]
        ON [identity].[email_verification_tokens]([token_hash]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM [sys].[indexes]
    WHERE [name] = N'IX_email_verification_tokens_user_created'
      AND [object_id] = OBJECT_ID(N'[identity].[email_verification_tokens]')
)
BEGIN
    CREATE INDEX [IX_email_verification_tokens_user_created]
        ON [identity].[email_verification_tokens]([user_id], [created_at]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM [sys].[indexes]
    WHERE [name] = N'UX_email_verification_tokens_active'
      AND [object_id] = OBJECT_ID(N'[identity].[email_verification_tokens]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_email_verification_tokens_active]
        ON [identity].[email_verification_tokens]([user_id])
        WHERE [consumed_at] IS NULL AND [revoked_at] IS NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM [sys].[foreign_keys]
    WHERE [name] = N'FK_email_verification_tokens_users'
      AND [parent_object_id] = OBJECT_ID(N'[identity].[email_verification_tokens]')
)
BEGIN
    ALTER TABLE [identity].[email_verification_tokens]
        ADD CONSTRAINT [FK_email_verification_tokens_users]
        FOREIGN KEY ([user_id]) REFERENCES [identity].[users]([id]) ON DELETE CASCADE;
END;
GO
