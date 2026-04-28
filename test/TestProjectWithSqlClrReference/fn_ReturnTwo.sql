-- This function is referenced from usp_SelectTwo to demonstrate how a reference to a non-clr function ends up in the model. 
CREATE FUNCTION [dbo].[fn_ReturnTwo] ()
RETURNS INT
as 
begin
return 2
end