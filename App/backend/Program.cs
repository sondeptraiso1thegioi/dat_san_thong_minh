using System.Text;
using System.Threading.RateLimiting;
using DatSan.Api.Config;
using DatSan.Api.Data;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Repositories.Implements;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using DatSan.Api.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Chạy scripts/setup-local.ps1 hoặc cấu hình ConnectionStrings__DefaultConnection.");
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32 || string.IsNullOrWhiteSpace(jwt.Issuer)
    || string.IsNullOrWhiteSpace(jwt.Audience) || jwt.LifetimeMinutes is < 1 or > 1440)
    throw new InvalidOperationException("Cấu hình Jwt chưa hợp lệ. Chạy scripts/setup-local.ps1 để tạo khóa local.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IVenueRepository, VenueRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<ISettingRepository, SettingRepository>();
builder.Services.AddScoped<IPlayerHomeService, PlayerHomeService>();
builder.Services.AddScoped<IVenueSearchService, VenueSearchService>();
builder.Services.AddScoped<IOwnerVenueRepository, OwnerVenueRepository>();
builder.Services.AddScoped<IOwnerVenueService, OwnerVenueService>();
builder.Services.AddScoped<IAdminVenueRepository, AdminVenueRepository>();
builder.Services.AddScoped<IAdminVenueService, AdminVenueService>();
builder.Services.AddScoped<IPlayerBookingRepository, PlayerBookingRepository>();
builder.Services.AddScoped<IPlayerBookingService, PlayerBookingService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>()).ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ApiError(
        context.ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage
            ?? "Thông tin không hợp lệ.", "VALIDATION_ERROR"));
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true, RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(5), NameClaimType = "name", RoleClaimType = "role",
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!long.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id))
            {
                context.Fail("Invalid subject");
                return;
            }
            var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await repository.FindByIdAsync(id, context.HttpContext.RequestAborted);
            if (user is null || user.Status != "active"
                || user.Role != context.Principal?.FindFirst("role")?.Value)
                context.Fail("Account no longer valid");
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new ApiError(
                "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.", "UNAUTHORIZED"));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new ApiError("Bạn không có quyền truy cập.", "FORBIDDEN"));
        }
    };
});
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new ApiError(
            "Bạn thử đăng nhập quá nhiều lần. Vui lòng đợi một phút.", "TOO_MANY_ATTEMPTS"), cancellationToken);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Sun Sport API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        _ = await db.Users.AnyAsync(cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception error) when (error is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
    {
        app.Logger.LogWarning("Database readiness check failed: {Type}", error.GetType().Name);
        return Results.Json(new ApiError("Chưa kết nối được dữ liệu.", "DATABASE_UNAVAILABLE"), statusCode: 503);
    }
}).AllowAnonymous();
app.Run();
