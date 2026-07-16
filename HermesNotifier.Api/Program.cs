using System.Text;
using HermesNotifier.Api.Auth;
using HermesNotifier.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var adminJwtSecret = builder.Configuration["ADMIN_JWT_SECRET"];
if (string.IsNullOrWhiteSpace(adminJwtSecret))
{
    if (builder.Environment.IsDevelopment())
    {
        adminJwtSecret = "dev-only-admin-jwt-secret-please-change";
        builder.Logging.AddConsole();
    }
    else
    {
        throw new InvalidOperationException("缺少 ADMIN_JWT_SECRET 設定，無法啟用管理員驗證。");
    }
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(adminJwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new AdminUserRequirement());
    });
});
builder.Services.AddScoped<IAuthorizationHandler, AdminUserRequirementHandler>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 啟動時自動套用 EF Core migrations（部署時自動更新資料庫結構）
// 用環境變數 AUTO_MIGRATE 控制，預設開啟；若要暫時關閉設為 "false"/"0"
var autoMigrate = builder.Configuration["AUTO_MIGRATE"];
if (string.IsNullOrWhiteSpace(autoMigrate)
    || (autoMigrate != "0"
        && !autoMigrate.Equals("false", StringComparison.OrdinalIgnoreCase)))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("套用資料庫 migrations 中…");
        db.Database.Migrate();
        logger.LogInformation("資料庫 migrations 套用完成。");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "套用資料庫 migrations 失敗。");
        throw; // 結構未更新時讓容器啟動失敗，避免在錯誤 schema 上運行
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
