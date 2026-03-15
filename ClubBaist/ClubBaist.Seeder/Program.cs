using ClubBaist.Domain;
using ClubBaist.Seeder;
using Microsoft.AspNetCore.Identity;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<ApplicationDbContext>("clubbaist");

builder.Services.AddIdentityCore<IdentityUser<Guid>>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddHostedService<SeedWorker>();

var host = builder.Build();
host.Run();
