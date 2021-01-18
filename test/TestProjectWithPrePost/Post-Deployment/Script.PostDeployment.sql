/*
Post-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.		
 Use SQLCMD syntax to include a file in the post-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the post-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/

PRINT 'Inserting record into MyTable'
INSERT INTO [dbo].[MyTable] VALUES ('SomeString', 1)

if SERVERPROPERTY('EngineEdition') > 4
begin

    -- Enable Azure connections to the database.  
    EXECUTE sp_set_database_firewall_rule N'Allow Azure', '0.0.0.0', '0.0.0.0';

    IF NOT EXISTS (SELECT [name]
                    FROM [sys].[database_principals]
                    WHERE [name] = N'DbUser')
    BEGIN
	    CREATE USER [DbUser]
	    WITH PASSWORD = '$(DbUserPassword)';

	    GRANT CONNECT TO [DbUser];

	    ALTER ROLE db_owner ADD MEMBER [DbUser];
    END

    IF NOT EXISTS (SELECT [name]
                    FROM [sys].[database_principals]
                    WHERE [name] = N'DbReader')
    BEGIN
	    CREATE USER [DbReader]
	    WITH PASSWORD = '$(DbReaderPassword)';

	    GRANT CONNECT TO [DbReader];

	    ALTER ROLE db_datareader ADD MEMBER [DbReader];
    END

end
