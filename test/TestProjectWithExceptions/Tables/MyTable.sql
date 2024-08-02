-- This file has deliberate errors (a GO is missing)
CREATE TABLE [dbo].[MyTable]
(
    Column1 nvarchar(100),
    Column2 int
);

CREATE INDEX MyIndex ON MyTable(Column2)
