using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql");
//db.WithLifetime(ContainerLifetime.Persistent);
    var sql = db.AddDatabase("clubbaist");
var seeder = builder.AddProject<Projects.ClubBaist_Seeder>("seeder")
    .WithReference(sql)
    .WaitFor(sql);

builder.AddProject<Projects.ClubBaist_Web>("web")
    .WithReference(sql)
    .WaitFor(seeder);

builder.Build().Run();
