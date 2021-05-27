if (@@error <> 0 and @@trancount > 0)
begin
    rollback tran;
end
GO

if @@trancount > 0 
begin
	print N'Post/Pre script finished';
	commit tran;
end
else 
	print N'Pre/Post scripts failed';
GO;