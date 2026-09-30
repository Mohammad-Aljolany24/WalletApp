using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WalletApp.Core.Auth;
using WalletApp.Core.Aggregates;
using WalletApp.Core.EventStore;
using WalletApp.Core.Projections;
using WalletApp.Core.Queries;
using WalletApp.Core.ReadModels;
using WalletApp.Data;
using WalletApp.Data.Auth;
using WalletApp.Data.EventStore;
using WalletApp.Data.ReadModels;


var builder = WebApplication.CreateBuilder(args);


// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "WalletApp API", Version = "v1" });

  options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
    Name = "Authorization",
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Paste your JWT token. Do NOT include 'Bearer' — Swagger adds it automatically."
});

options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
{
    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
});
});


// ============================================
// Database
// ============================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ============================================
// JWT config
// ============================================
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            NameClaimType = "sub"
        };

        // ← NEW: log authentication results
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine("❌ JWT FAILED: " + ctx.Exception.GetType().Name + " — " + ctx.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                Console.WriteLine("✅ JWT OK for: " + ctx.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });
    // Disable claim mapping globally
    System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddAuthorization();

// ============================================
// Auth services
// ============================================
builder.Services.AddScoped<IUserStore, SqlUserStore>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService>(_ =>
    new JwtTokenService(jwtSecret, jwtIssuer, jwtAudience));
builder.Services.AddScoped<IAuthService, AuthService>();

// ============================================
// Event sourcing services (Scoped — they use DbContext)
// ============================================
builder.Services.AddScoped<IEventStore, SqlEventStore>();
builder.Services.AddScoped<IWalletReadStore, SqlWalletReadStore>();
builder.Services.AddScoped<WalletProjection>();
builder.Services.AddScoped<GetBalanceQueryHandler>();

var app = builder.Build();

// Swagger UI (dev only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// AUTH ENDPOINTS (public)
// ============================================

app.MapPost("/auth/register", async (RegisterRequest req, IAuthService auth) =>
{
    var user = await auth.RegisterAsync(req.Email, req.Password);
    return Results.Ok(new { user.Id, user.Email });
});

app.MapPost("/auth/login", async (LoginRequest req, IAuthService auth) =>
{
    var token = await auth.LoginAsync(req.Email, req.Password);
    return Results.Ok(new { token });
});

// ============================================
// WALLET ENDPOINTS (protected)
// ============================================

static Guid GetUserId(HttpContext ctx)
{
    var sub = ctx.User.FindFirst("sub")?.Value
        ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    return Guid.Parse(sub!);
}

app.MapPost("/wallet/deposit", async (
    decimal amount,
    HttpContext ctx,
    IEventStore eventStore,
    WalletProjection projection) =>
{
    var userId = GetUserId(ctx);
    var events = await eventStore.GetEventsAsync(userId);
    var wallet = Wallet.Rehydrate(userId, events);

    wallet.Deposit(amount);

    foreach (var evt in wallet.UncommittedEvents)
    {
        await eventStore.AppendAsync(userId, evt);
        await projection.HandleAsync(evt);
    }

    return Results.Ok(new { balance = wallet.Balance });
}).RequireAuthorization();

app.MapPost("/wallet/withdraw", async (
    decimal amount,
    HttpContext ctx,
    IEventStore eventStore,
    WalletProjection projection) =>
{
    var userId = GetUserId(ctx);
    var events = await eventStore.GetEventsAsync(userId);
    var wallet = Wallet.Rehydrate(userId, events);

    wallet.Withdraw(amount);

    foreach (var evt in wallet.UncommittedEvents)
    {
        await eventStore.AppendAsync(userId, evt);
        await projection.HandleAsync(evt);
    }

    return Results.Ok(new { balance = wallet.Balance });
}).RequireAuthorization();

app.MapGet("/wallet/balance", async (
    HttpContext ctx,
    GetBalanceQueryHandler handler) =>
{
    var userId = GetUserId(ctx);
    var query = new GetBalanceQuery { WalletId = userId };
    var balance = await handler.HandleAsync(query);
    return Results.Ok(new { balance });
}).RequireAuthorization();

app.Run();

// ============================================
// Request DTOs
// ============================================
public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);

public partial class Program { }