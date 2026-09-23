-- Optional production maintenance, authorized: remove only old three-lap results.
DELETE FROM dbo.RaceResults WHERE Track IN ('ridge','suzuka');