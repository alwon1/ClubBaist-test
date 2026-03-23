using Azure.Provisioning.AppService;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);
var appServiceEnv = builder.AddAzureAppServiceEnvironment("app-service-env");
appServiceEnv.ConfigureInfrastructure(infra =>
{
    var resources = infra.GetProvisionableResources();
    var plan = resources.OfType<AppServicePlan>().Single();

    plan.Sku = new AppServiceSkuDescription
    {
        Name = "F1",
        Tier = "Free"
    };
});
var db = builder.AddAzureSqlServer("sql");
db.RunAsContainer();
//db.WithLifetime(ContainerLifetime.Persistent);
var sql = db.AddDatabase("clubbaist");
var seeder = builder.AddProject<Projects.ClubBaist_Seeder>("seeder")
    .WithReference(sql)
    .WaitFor(sql);

builder.AddProject<Projects.ClubBaist_Web>("web").PublishAsAzureAppServiceWebsite((infra, site) =>
    {
    }).WithExternalHttpEndpoints()
    .WithReference(sql)
    .WaitFor(seeder);

builder.Build().Run();
