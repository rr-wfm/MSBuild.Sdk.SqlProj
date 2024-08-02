-- This file has deliberate errors (a GO is missing)
CREATE TABLE [dbo].[MyTable2]
(
    Column1 nvarchar(100),
    Column2 int
);

CREATE INDEX MyIndex2 ON MyTable2(Column2)
