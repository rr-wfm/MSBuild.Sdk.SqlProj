if exists (select * from dim_numbers) 
	begin
		insert into dim_numbers (number) 
			select 
				max(number) + 1
			from dim_numbers
	end