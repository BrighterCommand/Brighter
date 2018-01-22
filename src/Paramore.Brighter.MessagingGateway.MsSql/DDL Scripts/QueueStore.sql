-- User Table information:
-- Number of tables: 1
-- Commands: 0 row(s)

PRINT 'Creating Queue table'
CREATE TABLE [dbo].[QueueData](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Topic] [nvarchar](255) NOT NULL,
	[MessageType] [nvarchar](1024) NOT NULL,
	[Payload] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_QueueData] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

PRINT 'Creating an index on the Topic column of the Queue table...'
CREATE NONCLUSTERED INDEX [IX_Topic] ON [dbo].[QueueData]
(
	[Topic] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

Print 'Done'