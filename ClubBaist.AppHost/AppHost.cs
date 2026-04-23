//using Azure.Provisioning.AppService;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);
var db = builder.AddSqlServer("sql").WithLifetime(ContainerLifetime.Session);
//var appServiceEnv = builder.AddAzureAppServiceEnvironment("app-service-env");
// appServiceEnv.ConfigureInfrastructure(infra =>
// {
//     var resources = infra.GetProvisionableResources();
//     var plan = resources.OfType<AppServicePlan>().Single();

//     plan.Sku = new AppServiceSkuDescription
//     {
//         Name = "F1",
//         Tier = "Free"
//     };
// });
//var db = builder.AddAzureSqlServer("sql");
//db.RunAsContainer();
//db.WithLifetime(ContainerLifetime.Persistent);
var sql = db.AddDatabase("clubbaist");


builder.AddProject<Projects.ClubBaist_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(sql)
    .WaitFor(db)
    .WaitFor(sql);

builder.Build().Run();
