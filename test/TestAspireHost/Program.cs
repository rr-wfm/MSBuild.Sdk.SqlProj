var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddDataTierApplication<Projects.TestProject>("testproject")
       .PublishTo(sql);

builder.AddDataTierApplication("testprojectwithwarnings", "../TestProjectWithWarnings/bin/Debug/netstandard2.0/TestProjectWithWarnings.dacpac")
       .PublishTo(sql);

builder.Build().Run();
