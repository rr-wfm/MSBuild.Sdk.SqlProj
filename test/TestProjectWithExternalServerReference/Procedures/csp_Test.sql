CREATE PROCEDURE [dbo].[csp_Test]
AS
BEGIN
    SELECT * FROM [$(SomeServer)].[SomeDatabase].[dbo].[MyTable];
END