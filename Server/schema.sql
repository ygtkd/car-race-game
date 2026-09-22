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
