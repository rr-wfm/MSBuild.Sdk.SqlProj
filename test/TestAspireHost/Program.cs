var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddSqlProject<Projects.TestProject>("testproject")
       .PublishTo(sql);

builder.AddSqlProject("testprojectwithwarnings")
       .FromDacpac("../TestProjectWithWarnings/bin/Debug/netstandard2.0/TestProjectWithWarnings.dacpac")
       .PublishTo(sql);

builder.Build().Run();
