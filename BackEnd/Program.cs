using BackEnd.Data;
using BackEnd.Entities;
using BackEnd.Endpoints;
using BackEnd.Hubs;
using BackEnd.Infrastructure;
using BackEnd.Options;
using BackEnd.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Options ─────────────────────────────────────────────────────────────────
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<GdalOptions>(builder.Configuration.GetSection("Gdal"));
builder.Services.Configure<TilesOptions>(builder.Configuration.GetSection("Tiles"));

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .AllowCredentials()));

// ── Exception Handling & ProblemDetails ──────────────────────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── DB ─────────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        o => o.UseNetTopologySuite()));

// ── Identity + Bearer Token ───────────────────────────────────────────────────
builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AdditionalUserClaimsPrincipalFactory>();

// AddIdentityApiEndpoints 已內部呼叫 AddBearerToken；此處僅設定 token 有效期
builder.Services.Configure<Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions>(
    IdentityConstants.BearerScheme,
    options => { options.BearerTokenExpiration = TimeSpan.FromHours(8); });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

// ── SignalR ────────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<FloorService>();
builder.Services.AddScoped<ITileStorage, TileStorage>();
builder.Services.AddScoped<SeatService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<TileConversionService>();
builder.Services.AddScoped<GdalRunner>();
builder.Services.AddSingleton<TileConversionChannel>();
builder.Services.AddHostedService<TileConversionWorker>();

// ── OpenAPI (Development) ─────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Startup: DB Migrate & Seed ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "DB 初始化失敗，請確認連線字串與 PostgreSQL 服務狀態");
    }
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();

// ── Health Check ──────────────────────────────────────────────────────────────
app.MapHealthChecks("/health").AllowAnonymous();

// ── Static Files for Tiles ────────────────────────────────────────────────────
var tilesRootPath = builder.Configuration["Tiles:RootPath"] ?? "./_data/tiles";
var tilesAbsolute = Path.GetFullPath(tilesRootPath);
if (!Directory.Exists(tilesAbsolute))
    Directory.CreateDirectory(tilesAbsolute);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(tilesAbsolute),
    RequestPath = "/tiles"
});

// ── SignalR Hub ───────────────────────────────────────────────────────────────
app.MapHub<TileConversionHub>("/hubs/tile-conversion");

// ── API Group ─────────────────────────────────────────────────────────────────
var api = app.MapGroup("/api");

// Identity 內建端點（/refresh、/logout 等）—— login 由自訂端點提供
api.MapGroup("/identity").MapIdentityApi<ApplicationUser>();

// 自訂端點（含 /auth/login、/auth/me）
api.MapAuthEndpoints();
api.MapFloorEndpoints();
api.MapFloorMapEndpoints();
api.MapSeatEndpoints();
api.MapEmployeeEndpoints();
api.MapAssignmentEndpoints();
api.MapAttendanceEndpoints();

app.Run();
