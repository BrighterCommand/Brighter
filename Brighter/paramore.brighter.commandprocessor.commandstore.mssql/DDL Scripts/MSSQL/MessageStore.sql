-- User Table information:
-- Number of tables: 1
-- Commands: 0 row(s)

CREATE TABLE [Commands] (
  [CommandId] uniqueidentifier NOT NULL
, [CommandType] nvarchar(256) NULL
, [CommandBody] ntext NULL
, [Timestamp] datetime NULL
);
GO
ALTER TABLE [Commands] ADD CONSTRAINT [PK_MessageId] PRIMARY KEY ([CommandId]);
GO
