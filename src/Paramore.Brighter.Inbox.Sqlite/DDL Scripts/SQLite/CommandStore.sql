SELECT 1;
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE [Commands] (
  [CommandId] uniqueidentifier NOT NULL
, [CommandType] nvarchar(256) NULL
, [CommandBody] ntext NULL
, [Timestamp] datetime NULL
, [ContextKey] nvarchar(256) NULL
, CONSTRAINT [PK_MessageId] PRIMARY KEY ([CommandId])
);
COMMIT;

