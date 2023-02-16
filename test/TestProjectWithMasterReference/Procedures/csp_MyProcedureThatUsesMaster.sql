CREATE PROCEDURE [dbo].[csp_MyProcedureThatUsesMaster]
    @p_SomeParam int
AS
BEGIN
    SELECT * FROM sys.master_files;
END