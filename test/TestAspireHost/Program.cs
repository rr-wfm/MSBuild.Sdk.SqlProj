var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer("sql", sqlPassword, port: 1234)
    .WithDataVolume("MyDataVolume");

var sqlDatabase = sqlServer.AddDatabase("Database");

var sqlProject = builder.AddSqlProject<Projects.TestProject>("testproject")
    .PublishTo(sqlDatabase);

var dabConfig = "./dab-config.json";
var dabServer = builder
    .AddContainer("data-api", "mcr.microsoft.com/azure-databases/data-api-builder")
    .WithBindMount(dabConfig, "/App/dab-config.json")
    .WithHttpEndpoint(port: 5000, targetPort: 5000, name: "http")
    .WithReference(sqlDatabase)
    .WaitForSqlProject(sqlProject);

builder.Build().Run();
