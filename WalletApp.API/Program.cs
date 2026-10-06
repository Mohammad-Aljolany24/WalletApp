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
using Microsoft.AspNetCore.Authorization;                                   
using WalletApp.API.Authorization;                                           
using WalletApp.Core.Auth.Requirements;    
using WalletApp.Core.Events;
using WalletApp.API.Exceptions;
using WalletApp.Core.Exceptions;
using WalletApp.API.Idempotency;
using WalletApp.Core.Pagination;
using Serilog;
using WalletApp.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});


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
         options.MapInboundClaims = false;   
          options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            NameClaimType = "sub",
             RoleClaimType = "role"  
        };

        // ← NEW: log authentication results
      options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = ctx =>
    {
        var logger = ctx.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("JwtBearer");
        logger.LogWarning(ctx.Exception, "JWT authentication failed");
        return Task.CompletedTask;
    },
    OnTokenValidated = ctx =>
    {
        var logger = ctx.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("JwtBearer");
        logger.LogInformation("JWT validated for {Subject}", ctx.Principal?.Identity?.Name);
        return Task.CompletedTask;
    }
};
    });
    // Disable claim mapping globally
    System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsAdmin", p =>
        p.RequireRole("Admin"));

    options.AddPolicy("CanTrade", p =>
        p.AddRequirements(new CanTradeRequirement()));

    options.AddPolicy("IsVerified", p =>
        p.AddRequirements(new IsVerifiedRequirement()));

    options.AddPolicy("AccountNotFrozen", p =>
        p.AddRequirements(new AccountNotFrozenRequirement()));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();



// ============================================
// Auth services
// ============================================
builder.Services.AddScoped<IUserStore, SqlUserStore>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService>(_ =>
    new JwtTokenService(jwtSecret, jwtIssuer, jwtAudience));
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IAuthorizationHandler, CanTradeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IsVerifiedHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AccountNotFrozenHandler>();

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


app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors("Frontend");

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

app.MapGet("/auth/me", async (
    HttpContext ctx,
    IUserStore userStore) =>
{
    var userId = GetUserId(ctx);
    var user = await userStore.GetByIdAsync(userId);
    if (user is null)
        return Results.NotFound(new { error = "User not found." });

    return Results.Ok(new
    {
        user.Id,
        user.Email,
        user.Role,
        user.IsVerified,
        user.IsFrozen
    });
}).RequireAuthorization();

// ============================================
// WALLET ENDPOINTS (protected)
// ============================================

static Guid GetUserId(HttpContext ctx)
{
    var sub = ctx.User.FindFirst("sub")?.Value
        ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    if (!Guid.TryParse(sub, out var userId))
        throw new UnauthorizedException("Missing or invalid user identity.");

    return userId;
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

    var expectedVersion = wallet.Version;   // <-- snapshot BEFORE mutating

    wallet.Deposit(amount);

       await eventStore.AppendAsync(userId, wallet.UncommittedEvents, expectedVersion);

    foreach (var evt in wallet.UncommittedEvents)
        await projection.HandleAsync(evt);

    wallet.ClearUncommittedEvents();

    return Results.Ok(new { balance = wallet.Balance });
}).AddEndpointFilter<IdempotencyFilter>()
.RequireAuthorization("AccountNotFrozen");

app.MapPost("/wallet/withdraw", async (
    decimal amount,
    HttpContext ctx,
    IEventStore eventStore,
    WalletProjection projection) =>
{
    var userId = GetUserId(ctx);
    var events = await eventStore.GetEventsAsync(userId);
    var wallet = Wallet.Rehydrate(userId, events);

    var expectedVersion = wallet.Version;   // <-- snapshot BEFORE mutating

    wallet.Withdraw(amount);

       await eventStore.AppendAsync(userId, wallet.UncommittedEvents, expectedVersion);

    foreach (var evt in wallet.UncommittedEvents)
        await projection.HandleAsync(evt);

    wallet.ClearUncommittedEvents();

    return Results.Ok(new { balance = wallet.Balance });
})
.AddEndpointFilter<IdempotencyFilter>()
.RequireAuthorization("CanTrade");

app.MapGet("/wallet/balance", async (
    HttpContext ctx,
    GetBalanceQueryHandler handler) =>
{
    var userId = GetUserId(ctx);
    var query = new GetBalanceQuery { WalletId = userId };
    var balance = await handler.HandleAsync(query);
    return Results.Ok(new { balance });
}).RequireAuthorization();


app.MapGet("/wallet/transactions", async (
    HttpContext ctx,
    IEventStore eventStore,
    string? cursor,
    int? limit) =>
{
    var userId = GetUserId(ctx);
    var take = Math.Clamp(limit ?? 20, 1, 100);

    long? afterId = null;
    if (cursor is not null)
    {
        afterId = Cursor.DecodeEventCursor(cursor);
        if (afterId is null)
            return Results.Ok(new { items = Array.Empty<object>(), nextCursor = (string?)null });
    }

    var paged = await eventStore.GetEventsPagedAsync(userId, afterId, take);

    var items = paged.Items
        .Select(s => s.Event switch
        {
            FundsDeposited d => new
            {
                Type = "Deposited",
                Amount = d.Amount,
                OccurredAt = d.OccurredAt
            },
            FundsWithdrawn w => new
            {
                Type = "Withdrawn",
                Amount = w.Amount,
                OccurredAt = w.OccurredAt
            },
            _ => null
        })
        .Where(t => t is not null)
        .ToList();

    return Results.Ok(new { items, nextCursor = paged.NextCursor });
}).RequireAuthorization();




// ============================================
// ADMIN ENDPOINTS
// ============================================

app.MapPost("/admin/users/{id:guid}/verify", async (
    Guid id,
    IUserStore userStore) =>
{
    var user = await userStore.GetByIdAsync(id);
    if (user is null)
        return Results.NotFound(new { error = "User not found." });

    user.IsVerified = true;
    await userStore.SaveAsync(user);

    return Results.Ok(new { user.Id, user.IsVerified });
}).RequireAuthorization("IsAdmin");

app.MapPost("/admin/users/{id:guid}/freeze", async (
    Guid id,
    IUserStore userStore) =>
{
    var user = await userStore.GetByIdAsync(id);
    if (user is null)
        return Results.NotFound(new { error = "User not found." });

    user.IsFrozen = true;
    await userStore.SaveAsync(user);

    return Results.Ok(new { user.Id, user.IsFrozen });
}).RequireAuthorization("IsAdmin");

app.MapGet("/admin/users", async (
    IUserStore userStore,
    string? cursor,
    int? limit) =>
{
    var take = Math.Clamp(limit ?? 20, 1, 100);

    DateTime? afterCreatedAt = null;
    Guid? afterId = null;

    if (cursor is not null)
    {
        var decoded = Cursor.DecodeUserCursor(cursor);
        if (decoded is null)
            return Results.Ok(new { items = Array.Empty<object>(), nextCursor = (string?)null });

        afterCreatedAt = decoded.Value.CreatedAt;
        afterId = decoded.Value.Id;
    }

    var paged = await userStore.GetPagedAsync(afterCreatedAt, afterId, take);

    var items = paged.Items.Select(u => new
    {
        u.Id,
        u.Email,
        u.Role,
        u.IsVerified,
        u.IsFrozen,
        u.CreatedAt
    }).ToList();

    return Results.Ok(new { items, nextCursor = paged.NextCursor });
}).RequireAuthorization("IsAdmin");





try
{
    Log.Information("Starting WalletApp API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WalletApp API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// ============================================
// Request DTOs
// ============================================
public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);

public partial class Program { }