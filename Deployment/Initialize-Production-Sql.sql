-- Run as the Microsoft Entra administrator in takeda-free-database.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.RaceResults', N'U') IS NULL
BEGIN
CREATE TABLE dbo.RaceResults (
 RaceId varchar(32) NOT NULL,
 PlayerId varchar(32) NOT NULL,
 Name nvarchar(32) NOT NULL,
 Track varchar(16) NOT NULL,
 Rank int NOT NULL,
 Time real NOT NULL,
 BestLap real NOT NULL,
 Dnf bit NOT NULL,
 FinishedAt datetime2 NOT NULL,
 CONSTRAINT PK_RaceResults PRIMARY KEY (RaceId, PlayerId)
);
CREATE INDEX IX_RaceResults_TrackTime ON dbo.RaceResults(Track,Dnf,Time);

END;
IF DATABASE_PRINCIPAL_ID(N'car-race-game') IS NULL
    CREATE USER [car-race-game] FROM EXTERNAL PROVIDER;
GRANT SELECT, INSERT ON OBJECT::dbo.RaceResults TO [car-race-game];
COMMIT;
SELECT name, type_desc FROM sys.database_principals WHERE name=N'car-race-game';
SELECT TOP (1) * FROM dbo.RaceResults;
