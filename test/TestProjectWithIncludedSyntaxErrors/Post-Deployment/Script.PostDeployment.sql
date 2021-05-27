/*
 Post-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be executed after the build script.	
 Use SQLCMD syntax to include a file in the post-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the post-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/

print '____________________________';
print 'Refreshing Inserts';
print '____________________________';
print ' ';
:r DataScript.sql

/* Finalize scripts */ 
:r "transaction_end.sql" 