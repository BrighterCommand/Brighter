-- THE DDL SCRIPT FROM MSSQL OUTBOX
PRINT 'Creating Messages table';
CREATE TABLE [BrighterOutbox]
(
    [Id] [BIGINT] NOT NULL IDENTITY ,
    [MessageId] UNIQUEIDENTIFIER NOT NULL ,
    [Topic] NVARCHAR(255) NULL ,
    [MessageType] NVARCHAR(32) NULL ,
    [Timestamp] DATETIME NULL ,
    [CorrelationId] UNIQUEIDENTIFIER NULL,
    [ReplyTo] NVARCHAR(255) NULL,
    [ContentType] NVARCHAR(128) NULL,
    [Dispatched] DATETIME NULL ,
    [HeaderBag] NTEXT NULL ,
    [Body] NTEXT NULL ,
    PRIMARY KEY ( [Id] )
    );
GO
IF ( NOT EXISTS ( SELECT    *
                  FROM      sys.indexes
                  WHERE     name = 'UQ_BrighterOutbox__MessageId'
                            AND object_id = OBJECT_ID('BrighterOutbox') )
   )
BEGIN
        PRINT 'Creating a unique index on the MessageId column of the BrighterOutbox table...';

        CREATE UNIQUE NONCLUSTERED INDEX BrighterOutbox
        ON BrighterOutbox(MessageId);
END;
GO
IF ( NOT EXISTS
(
       SELECT *
       FROM   sys.indexes
       WHERE  name = 'BrighterOutbox'
       AND    object_id = Object_id('BrighterOutbox') ) )
BEGIN
  PRINT 'Creating a non-unique index on the Dispatched column of the BrighterOutbox table...';
  CREATE NONCLUSTERED INDEX BrighterOutbox
  ON BrighterOutbox(Dispatched ASC);
END;
GO
PRINT 'Done';

-- THE ORDER SCHEMA
CREATE TABLE [Orders]
(
    [Id] [BIGINT] NOT NULL IDENTITY ,
    [Number] varchar(max) NOT NULL ,
    [Version] int NOT NULL ,
    [Type] int NOT NULL ,
    [ActionsPending] bit NOT NULL,
    [Status] int NOT NULL
    PRIMARY KEY ( [Id] )
    );
GO