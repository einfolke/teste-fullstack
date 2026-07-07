
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Parking.Api.Data;
using Parking.Api.Services;
using Parking.Api.Validators;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("Postgres")
           ?? "Host=localhost;Port=5432;Database=parking_test;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(conn);
});

builder.Services.AddScoped<PlacaService>();
builder.Services.AddScoped<FaturamentoService>();
builder.Services.AddScoped<FaturamentoJob>();

// Lock distribuído: usa Redis se houver connection string "Redis"; senão, NoOp (sem coordenação).
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
{
    var options = ConfigurationOptions.Parse(redisConn);
    options.AbortOnConnectFail = false; // não derruba a app se o Redis estiver indisponível
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(options));
    builder.Services.AddSingleton<IDistributedLock, RedisDistributedLock>();
}
else
{
    builder.Services.AddSingleton<IDistributedLock, NoOpDistributedLock>();
}

builder.Services.AddValidatorsFromAssemblyContaining<ClienteCreateDtoValidator>();

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(conn)));
builder.Services.AddHangfireServer();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Parking API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // porta do Vite/React
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

// Agendamento recorrente: gera as faturas do mês anterior todo dia 1º às 02h (UTC)
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<FaturamentoJob>(
        "faturamento-mensal",
        job => job.GerarMesAnteriorAsync(CancellationToken.None),
        Cron.Monthly(1, 2));
}

app.MapControllers();

app.Run();
