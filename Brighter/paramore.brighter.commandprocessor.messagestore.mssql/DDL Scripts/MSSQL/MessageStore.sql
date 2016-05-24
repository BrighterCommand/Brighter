-- User Table information:
-- Number of tables: 1
-- Messages: 0 row(s)

PRINT 'Creating Messages table'
CREATE TABLE [Messages] (
  [Id] [BIGINT] NOT NULL IDENTITY
, [MessageId] uniqueidentifier NOT NULL
, [Topic] nvarchar(255) NULL
, [MessageType] nvarchar(32) NULL
, [Timestamp] datetime NULL
, [HeaderBag] ntext NULL
, [Body] ntext NULL
, PRIMARY KEY ( [Id] )
);
GO
IF (NOT EXISTS ( SELECT    *
                  FROM      sys.indexes
                  WHERE     name = 'UQ_Messages__MessageId'
                            AND object_id = OBJECT_ID('Messages') )
   )
BEGIN
    PRINT 'Creating a unique index on the MessageId column of the Messages table...'

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Messages__MessageId
    ON Messages(MessageId)
END
GO
PRINT 'Done'