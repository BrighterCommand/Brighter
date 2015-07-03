-- User Table information:
-- Number of tables: 1
-- Messages: 0 row(s)

CREATE TABLE [Messages] (
  [MessageId] uniqueidentifier NOT NULL
, [Topic] nvarchar(255) NULL
, [MessageType] nvarchar(32) NULL
, [Timestamp] datetime NULL
, [HeaderBag] ntext NULL
, [Body] ntext NULL
);
GO
ALTER TABLE [Messages] ADD CONSTRAINT [PK_MessageId] PRIMARY KEY ([MessageId]);
GO
