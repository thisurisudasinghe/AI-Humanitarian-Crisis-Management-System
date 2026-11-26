-- ============================================================================
-- 12_ai_decision_uniqueness.sql - one human decision per AI recommendation
-- ============================================================================
-- Guarded, additive and repeatable. Never deletes historical records.
-- Run after 03_ai.sql. Safe to re-run.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Only create the unique index when no duplicate recommendation decisions exist.
IF NOT EXISTS (
    SELECT 1
    FROM [ai].[decision_records]
    GROUP BY [recommendation_id]
    HAVING COUNT(*) > 1
)
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_decision_records_recommendation_id'
      AND [object_id] = OBJECT_ID(N'[ai].[decision_records]'))
BEGIN
    CREATE UNIQUE INDEX [UX_decision_records_recommendation_id]
        ON [ai].[decision_records] ([recommendation_id]);
END;
GO
