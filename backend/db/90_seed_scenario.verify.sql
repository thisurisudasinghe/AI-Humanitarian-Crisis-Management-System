-- ============================================================================
-- 90_seed_scenario.verify.sql - DEV2 (D2-12) read-only seed verification
-- ============================================================================
-- Owner: Developer 2 (M2 Crisis & Case Management)
-- Run after: 00_common.sql, 01_cases.sql, 98_foreign_keys.sql,
--            99_seed_users.sql, API demo user seed, 90_seed_scenario.sql,
--            91_seed_ops.sql.
--
-- This script performs SELECTs only. It is a verification aid, not UAT
-- evidence. Record the actual query output in the UAT document only after a
-- real clean-database run.
-- ============================================================================

SET NOCOUNT ON;

WITH ExpectedRequest(id) AS (
    SELECT * FROM (VALUES
        ('1d000000-0000-0000-0000-000000000001'),
        ('1d000000-0000-0000-0000-000000000002'),
        ('1d000000-0000-0000-0000-000000000003'),
        ('1d000000-0000-0000-0000-000000000004'),
        ('1d000000-0000-0000-0000-000000000005'),
        ('1d000000-0000-0000-0000-000000000006'),
        ('1d000000-0000-0000-0000-000000000007'),
        ('1d000000-0000-0000-0000-000000000008'),
        ('1d000000-0000-0000-0000-000000000009'),
        ('1d000000-0000-0000-0000-000000000010')
    ) AS v(id)
),
Checks(check_name, expected, actual) AS (
    SELECT N'cases.crises fixed fixture', 1,
        (SELECT COUNT(*) FROM [cases].[crises]
         WHERE [id] = '1a000000-0000-0000-0000-000000000001')
    UNION ALL
    SELECT N'cases.areas fixed fixtures', 3,
        (SELECT COUNT(*) FROM [cases].[areas]
         WHERE [id] IN ('1b000000-0000-0000-0000-000000000001',
                        '1b000000-0000-0000-0000-000000000002',
                        '1b000000-0000-0000-0000-000000000003'))
    UNION ALL
    SELECT N'cases.households fixed fixture', 1,
        (SELECT COUNT(*) FROM [cases].[households]
         WHERE [id] = '1c000000-0000-0000-0000-000000000001')
    UNION ALL
    SELECT N'cases.requests fixed fixtures', 10,
        (SELECT COUNT(*) FROM ExpectedRequest e
         INNER JOIN [cases].[requests] r ON r.[id] = e.id)
    UNION ALL
    SELECT N'cases.request_status_history rows', 36,
        (SELECT COUNT(*) FROM [cases].[request_status_history]
         WHERE [id] LIKE '1e000000-%')
    UNION ALL
    SELECT N'cases.request_links merge fixture', 1,
        (SELECT COUNT(*) FROM [cases].[request_links]
         WHERE [id] = '1f000000-0000-0000-0000-000000000001')
    UNION ALL
    SELECT N'cases.attachments metadata fixtures', 2,
        (SELECT COUNT(*) FROM [cases].[attachments]
         WHERE [id] IN ('20000000-0000-0000-0000-000000000001',
                        '20000000-0000-0000-0000-000000000002'))
    UNION ALL
    SELECT N'cases.alerts fixtures', 2,
        (SELECT COUNT(*) FROM [cases].[alerts]
         WHERE [id] IN ('21000000-0000-0000-0000-000000000001',
                        '21000000-0000-0000-0000-000000000002'))
    UNION ALL
    SELECT N'cases.alerts published only', 1,
        (SELECT COUNT(*) FROM [cases].[alerts]
         WHERE [id] LIKE '21000000-%' AND [status] = N'Published')
    UNION ALL
    SELECT N'cases.alerts draft only', 1,
        (SELECT COUNT(*) FROM [cases].[alerts]
         WHERE [id] LIKE '21000000-%' AND [status] = N'Draft')
    UNION ALL
    SELECT N'cases.service_centres fixtures', 3,
        (SELECT COUNT(*) FROM [cases].[service_centres]
         WHERE [id] LIKE '22000000-%')
    UNION ALL
    SELECT N'cases.service_centres active only', 2,
        (SELECT COUNT(*) FROM [cases].[service_centres]
         WHERE [id] LIKE '22000000-%' AND [is_active] = 1)
    UNION ALL
    SELECT N'cases.feedback fixture', 1,
        (SELECT COUNT(*) FROM [cases].[feedback]
         WHERE [id] = '23000000-0000-0000-0000-000000000001')
    UNION ALL
    SELECT N'cases.partner_shares fixture', 1,
        (SELECT COUNT(*) FROM [cases].[partner_shares]
         WHERE [id] = '24000000-0000-0000-0000-000000000001')
    UNION ALL
    SELECT N'cases.requests address column', 1,
        CASE WHEN COL_LENGTH(N'cases.requests', N'address') IS NULL THEN 0 ELSE 1 END
    UNION ALL
    SELECT N'plaintext sensitive seed values', 0,
        (SELECT COUNT(*) FROM (
            SELECT [accessibility_needs] AS sensitive_value
            FROM [cases].[households]
            WHERE [id] = '1c000000-0000-0000-0000-000000000001'
            UNION ALL
            SELECT [accessibility_needs] AS sensitive_value
            FROM [cases].[requests]
            WHERE [id] LIKE '1d000000-%'
        ) AS s
        WHERE s.sensitive_value IS NOT NULL)
        + (SELECT COUNT(*) FROM [cases].[households]
           WHERE [id] = '1c000000-0000-0000-0000-000000000001'
             AND [contact_phone] IS NOT NULL)
    UNION ALL
    SELECT N'requests with Critical urgency', 2,
        (SELECT COUNT(*) FROM [cases].[requests]
         WHERE [id] LIKE '1d000000-%' AND [urgency] = N'Critical')
    UNION ALL
    SELECT N'shortage group R6-R8 Food', 3,
        (SELECT COUNT(*) FROM [cases].[requests]
         WHERE [id] IN ('1d000000-0000-0000-0000-000000000006',
                        '1d000000-0000-0000-0000-000000000007',
                        '1d000000-0000-0000-0000-000000000008')
           AND [category] = N'Food'
           AND [urgency] IN (N'Critical', N'High'))
    UNION ALL
    SELECT N'back-dated Submitted request R10', 1,
        (SELECT COUNT(*) FROM [cases].[requests]
         WHERE [id] = '1d000000-0000-0000-0000-000000000010'
           AND [status] = N'Submitted'
           AND [submitted_at] < DATEADD(HOUR, -24, SYSUTCDATETIME()))
)
SELECT
    check_name,
    expected,
    actual,
    CASE WHEN actual = expected THEN N'PASS' ELSE N'FAIL' END AS result
FROM Checks
ORDER BY check_name;

-- Status and urgency distributions for the 10 fixed requests. No PASS/FAIL is
-- asserted here; compare these rows with the expected fixture comments in
-- 90_seed_scenario.sql.
SELECT [status], COUNT(*) AS request_count
FROM [cases].[requests]
WHERE [id] LIKE '1d000000-%'
GROUP BY [status]
ORDER BY [status];

SELECT [urgency], COUNT(*) AS request_count
FROM [cases].[requests]
WHERE [id] LIKE '1d000000-%'
GROUP BY [urgency]
ORDER BY [urgency];
