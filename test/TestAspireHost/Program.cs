var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddDataTierApplication<Projects.TestProject>("db")
       .PublishTo(sql);

builder.Build().Run();
