using BackEnd.Data;
using BackEnd.Endpoints;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using BackEnd.Options;
using BackEnd.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

// ── MaxRev.Gdal native 初始化（一次性，須在任何 OGR 使用前） ───────────────────
MaxRev.Gdal.Core.GdalBase.ConfigureAll();
OSGeo.OGR.Ogr.RegisterAll();

var builder = WebApplication.CreateBuilder(args);

// ── Options ─────────────────────────────────────────────────────────────────
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<MapStorageOptions>(builder.Configuration.GetSection("MapStorage"));
builder.Services.Configure<GeoJsonConversionOptions>(builder.Configuration.GetSection("GeoJsonConversion"));

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE")));

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

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<FloorService>();
builder.Services.AddScoped<FloorMapStorage>();
builder.Services.AddScoped<IFloorMapStorage>(sp => sp.GetRequiredService<FloorMapStorage>());
builder.Services.AddScoped<DxfToGeoJsonConverter>();
builder.Services.AddScoped<FloorMapService>();
builder.Services.AddScoped<SeatService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddScoped<AttendanceService>();

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

// ── Static Files for GeoJSON 底圖 ─────────────────────────────────────────────
var mapStorageRoot = builder.Configuration["MapStorage:RootPath"] ?? "./_data";
var mapsAbsolute = Path.GetFullPath(Path.Combine(mapStorageRoot, "maps"));
Directory.CreateDirectory(mapsAbsolute);

var geoJsonContentType = new FileExtensionContentTypeProvider();
geoJsonContentType.Mappings[".geojson"] = "application/geo+json";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mapsAbsolute),
    RequestPath = "/maps",
    ContentTypeProvider = geoJsonContentType
});

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
