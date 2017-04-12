-- User Table information:
-- Number of tables: 1
-- Commands: 0 row(s)

PRINT 'Creating Commands table'
CREATE TABLE [Commands] (
  [Id] [BIGINT] NOT NULL IDENTITY
, [CommandId] uniqueidentifier NOT NULL
, [CommandType] nvarchar(256) NULL
, [CommandBody] ntext NULL
, [Timestamp] datetime NULL
, PRIMARY KEY ( [Id] )
);
GO
IF (NOT EXISTS ( SELECT    *
                  FROM      sys.indexes
                  WHERE     name = 'UQ_Commands__CommandId'
                            AND object_id = OBJECT_ID('Commands') )
   )
BEGIN
    PRINT 'Creating a unique index on the CommandId column of the Command table...'

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Commands__CommandId
    ON Commands(CommandId)
END
GO
Print 'Done'