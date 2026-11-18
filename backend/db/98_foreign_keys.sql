-- ============================================================================
-- 98_foreign_keys.sql - guarded cross-module foreign keys
-- ============================================================================
-- Runs after all module scripts (01..08) and before 99_seed_users.sql.
-- Every block is guarded so a missing table, missing column or already-present
-- relationship is skipped instead of failing the whole database setup.
--
-- Ownership reminder:
--   Dev 1: identity, common, don, notif
--   Dev 2: cases
--   Dev 3: ai
--   Dev 4: res, shelter, vol, geo
--
-- A foreign key that references another module's table belongs here, not in the
-- owning module's script, so module scripts stay independent.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ----------------------------------------------------------------------------
-- common.notifications.recipient_user_id -> identity.users.id
-- Both tables are Dev 1-owned, so 00_common.sql normally creates this foreign
-- key inline. This guarded block repairs databases created before the
-- notification table gained its recipient foreign key, and is a no-op when the
-- relationship is already present.
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'[common].[notifications]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[identity].[users]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[common].[notifications]', N'recipient_user_id') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.foreign_keys AS fk
       INNER JOIN sys.foreign_key_columns AS fkc
           ON fkc.constraint_object_id = fk.object_id
       WHERE fk.parent_object_id = OBJECT_ID(N'[common].[notifications]')
         AND fk.referenced_object_id = OBJECT_ID(N'[identity].[users]')
         AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = N'recipient_user_id')
BEGIN
    ALTER TABLE [common].[notifications] WITH CHECK
        ADD CONSTRAINT [FK_notifications_users]
        FOREIGN KEY ([recipient_user_id]) REFERENCES [identity].[users] ([id])
        ON DELETE CASCADE;

    ALTER TABLE [common].[notifications]
        CHECK CONSTRAINT [FK_notifications_users];
END;
GO

-- ----------------------------------------------------------------------------
-- Extension pattern for future cross-module relationships
-- ----------------------------------------------------------------------------
-- Copy this shape for each new relationship. Keep the guards: module scripts
-- may not exist yet, and a table may already declare the same relationship.
--
-- IF OBJECT_ID(N'[<child_schema>].[<child_table>]', N'U') IS NOT NULL
--    AND OBJECT_ID(N'[<parent_schema>].[<parent_table>]', N'U') IS NOT NULL
--    AND COL_LENGTH(N'[<child_schema>].[<child_table>]', N'<child_column>') IS NOT NULL
--    AND NOT EXISTS (
--        SELECT 1
--        FROM sys.foreign_keys AS fk
--        INNER JOIN sys.foreign_key_columns AS fkc
--            ON fkc.constraint_object_id = fk.object_id
--        WHERE fk.parent_object_id = OBJECT_ID(N'[<child_schema>].[<child_table>]')
--          AND fk.referenced_object_id = OBJECT_ID(N'[<parent_schema>].[<parent_table>]')
--          AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = N'<child_column>')
-- BEGIN
--     ALTER TABLE [<child_schema>].[<child_table>] WITH CHECK
--         ADD CONSTRAINT [FK_<child_table>_<parent_table>]
--         FOREIGN KEY ([<child_column>])
--         REFERENCES [<parent_schema>].[<parent_table>] ([<parent_column>])
--         ON DELETE NO ACTION;
--
--     ALTER TABLE [<child_schema>].[<child_table>]
--         CHECK CONSTRAINT [FK_<child_table>_<parent_table>];
-- END;
-- GO
