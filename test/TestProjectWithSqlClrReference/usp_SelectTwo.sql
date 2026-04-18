-- This file is to demonstrate how a reference to a non-clr function ends up in the model. 
create proc dbo.usp_SelectTwo
as
select dbo.fn_ReturnTwo()
