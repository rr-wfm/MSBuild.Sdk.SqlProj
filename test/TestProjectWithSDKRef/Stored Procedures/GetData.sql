CREATE PROCEDURE [dbo].[GetData]
AS
BEGIN
    SELECT  [Id], [Column1]
    FROM    [dbo].[TestTable];
END