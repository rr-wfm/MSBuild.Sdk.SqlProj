CREATE FUNCTION [dbo].[Function1]
(
	@param1 int,
	@param2 char(5)
)
RETURNS TABLE AS RETURN
(
	SELECT @param1 AS c1,
		   @param2 AS c2
)
