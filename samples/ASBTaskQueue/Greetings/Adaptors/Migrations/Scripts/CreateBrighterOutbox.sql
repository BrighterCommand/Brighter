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
        PRINT 'Creating a unique index on the MessageId column of the Messages table...';

        CREATE UNIQUE NONCLUSTERED INDEX UQ_BrighterOutbox__MessageId
        ON [BrighterOutbox] (MessageId);
END;
GO
PRINT 'Done';