var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .AddDatabase("clubbaist");

var seeder = builder.AddProject<Projects.ClubBaist_Seeder>("seeder")
    .WithReference(sql)
    .WaitFor(sql);

builder.AddProject<Projects.ClubBaist_Web>("web")
    .WithReference(sql)
    .WaitFor(seeder);

builder.Build().Run();
