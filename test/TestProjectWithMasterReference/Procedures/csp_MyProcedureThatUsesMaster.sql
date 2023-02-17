CREATE PROCEDURE [dbo].[csp_MyProcedureThatUsesMaster]
AS
BEGIN
    SELECT * FROM sys.master_files;
END