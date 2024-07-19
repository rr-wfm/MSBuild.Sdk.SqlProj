var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddDatabaseProject<Projects.TestProject>("db")
       .DeployTo(sql);

builder.Build().Run();
