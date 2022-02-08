using Elsa;
using Elsa.Persistence.EntityFramework.Core.Extensions;
using Elsa.Persistence.EntityFramework.PostgreSql;
using Elsa.Caching.Rebus.Extensions;
using Medallion.Threading.Postgres;
using Rebus.Config;
using Quartz;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
IConfigurationSection elsaSection = builder.Configuration.GetSection("Elsa");

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddElsaApiEndpoints();
_ = builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Demo", Version = "v1" }));

builder.Services.AddElsa(elsa => elsa
    .UseEntityFrameworkPersistence(ef => ef.UsePostgreSql("Host=localhost:15432;Username=postgres;Password=password;Database=postgres"))
    .AddConsoleActivities()
    .AddHttpActivities(elsaSection.GetSection("Server").Bind)
    .AddActivitiesFrom<Program>()
    .AddJavaScriptActivities()
    .UseServiceBus(context =>
        {
            _ = context.Configurer.Transport(
                transport => transport.UsePostgreSql(
                    "Host=localhost:15432;Username=postgres;Password=password;Database=postgres",
                    "rebus_service_transport",
                    context.QueueName
                )
            );
            _ = context.Configurer.Subscriptions(
                subscription => subscription.StoreInPostgres(
                    "Host=localhost:15432;Username=postgres;Password=password;Database=postgres",
                    "rebus_service_subscription"
                )
            );
        }
    )
    .ConfigureDistributedLockProvider(options =>
        options.UseProviderFactory(sp =>
            name => new PostgresDistributedLock(
                new PostgresAdvisoryLockKey("workflow", allowHashing: true),
                "Host=localhost:15432;Username=postgres;Password=password;Database=postgres",
                options: option => option.KeepaliveCadence(TimeSpan.FromSeconds(30))
            )
        )
    )
    .UseRebusCacheSignal()
    .AddQuartzTemporalActivities(
        configureQuartz: quartz => quartz.UsePersistentStore(store =>
        {
            store.UseProperties = true;
            store.UseJsonSerializer();
            store.UsePostgres("Host=localhost:15432;Username=postgres;Password=password;Database=postgres");
            store.UseClustering();
        }
        )
    )
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    _ = app.UseDeveloperExceptionPage();
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Demo v1"));

    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

_ = app
    .UseHttpActivities()
    .UseRouting()
    .UseAuthorization()
    // Elsa API Endpoints are implemented as regular ASP.NET Core API controllers.
    .UseEndpoints(endpoints =>
    {
        // Elsa API Endpoints are implemented as regular ASP.NET Core API controllers.
        endpoints.MapControllers();

        // For Dashboard.
        endpoints.MapFallbackToPage("/_Host");
    });

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
