using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Observability;
using ProductService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMyOpenTelemetry("ProductService");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

builder.Services.AddHealthChecks()
    // Self‑check — всегда Healthy, если сервис жив
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "self" })

    // Пример зависимости
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "sqlserver",
        timeout: TimeSpan.FromSeconds(1)
    );



builder.Services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.Zero; // без задержки перед первой публикацией
    options.Period = TimeSpan.FromSeconds(5); // обновление каждые 5 секунд
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.MapControllers();


app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Name == "self"
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true // все проверки, включая зависимости
});


using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    context.Database.EnsureCreated();
}

app.Run();