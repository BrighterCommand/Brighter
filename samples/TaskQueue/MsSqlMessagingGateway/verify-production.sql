-- Run in the intended deployment database. This script reads configuration only.
-- It does not validate pooled application sessions or change database settings.
SET NOCOUNT ON;

SELECT
    SERVERPROPERTY('ProductVersion') AS ProductVersion,
    SERVERPROPERTY('Edition') AS Edition,
    DB_NAME() AS DatabaseName,
    collation_name AS DatabaseCollation,
    compatibility_level AS CompatibilityLevel,
    is_read_committed_snapshot_on AS ReadCommittedSnapshotEnabled,
    SCHEMA_NAME() AS DefaultSchema
FROM sys.databases
WHERE database_id = DB_ID();

IF EXISTS (
    SELECT 1 FROM sys.databases
    WHERE database_id = DB_ID() AND is_read_committed_snapshot_on = 1
)
BEGIN
    THROW 51000, 'The MS SQL queue uses READPAST without READCOMMITTEDLOCK and does not support READ_COMMITTED_SNAPSHOT. Do not deploy this transport with that database setting.', 1;
END;

SELECT 'READ_COMMITTED_SNAPSHOT is disabled. Application scopes sharing the consumer pool must use locking Read Committed or Repeatable Read isolation.' AS IsolationRequirement;
