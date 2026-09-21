using System.Text;
using AspNetCoreRateLimit;
using CoreBanking.Application.Interfaces;
using CoreBanking.Application.Services;
using CoreBanking.Domain.Interfaces;
using CoreBanking.Infrastructure.Messaging;
using CoreBanking.Infrastructure.Outbox;
using CoreBanking.Infrastructure.Payments;
using CoreBanking.Infrastructure.Persistence;
using CoreBanking.Infrastructure.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Load .env if present
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
if (File.Exists(envPath)) Env.Load(envPath);
else if (File.Exists(".env")) Env.Load(".env");

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Database
var connectionString = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=corebanking;Username=postgres;Password=postgres";

builder.Services.AddDbContext<BankingDbContext>(opts =>
    opts.UseNpgsql(connectionString));

// Repositories & UoW
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ILoanRepository, LoanRepository>();
builder.Services.AddScoped<IDisputeRepository, DisputeRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Application services
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();
builder.Services.AddScoped<ICreditScoreService, CreditScoreService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// Paystack HTTP client
builder.Services.AddHttpClient<IPaystackService, PaystackService>();

// Kafka
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

// Outbox background processor
builder.Services.AddHostedService<OutboxProcessor>();

// ========== JWT Authentication ==========
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS!!";
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "CoreBanking";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "CoreBankingClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new() { Endpoint = "*:/api/v1/auth/login", Period = "1m", Limit = 10 },
        new() { Endpoint = "*:/api/v1/auth/register", Period = "1m", Limit = 5 },
        new() { Endpoint = "*:/api/v1/payments/*", Period = "1m", Limit = 30 },
        new() { Endpoint = "*:/api/v1/payments/initialize", Period = "1m", Limit = 10 },
        new() { Endpoint = "*", Period = "1m", Limit = 100 }
    };
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Core Banking System API",
        Version = "v1",
        Description = "JWT-secured core banking API with Paystack, Kafka, Outbox, Loans"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString);

var app = builder.Build();

// Auto-migrate / ensure created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch
    {
        await db.Database.EnsureCreatedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
