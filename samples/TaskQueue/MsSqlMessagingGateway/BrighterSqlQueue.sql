-- Creates the database these four sample applications share; the tables create themselves at
-- startup, so they are not here. Letting SQL Server choose the file locations is what lets this
-- run on Windows, Linux or a container.
--
--   sqlcmd -S <server> -U sa -P <password> -C -i BrighterSqlQueue.sql
--
-- Then run GreetingsSender and GreetingsReceiverConsole; each creates the tables it needs.

IF DB_ID('BrighterSqlQueue') IS NULL
BEGIN
    CREATE DATABASE [BrighterSqlQueue];
END
GO
