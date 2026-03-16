using ClubBaist.Domain;
using ClubBaist.Seeder;
using Microsoft.AspNetCore.Identity;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<ApplicationDbContext>("clubbaist");

builder.Services.AddIdentityCore<IdentityUser<Guid>>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

builder.Services.AddHostedService<SeedWorker>();

var host = builder.Build();
host.Run();
