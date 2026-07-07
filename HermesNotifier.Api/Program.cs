using HermesNotifier.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add Output Caching (僅用於 products list)
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("ProductsList", builder =>
        builder.Expire(TimeSpan.FromMinutes(30)) // 快取 30 分鐘
               .SetVaryByQuery("onlyExpired", "category") // 依 onlyExpired 與 category 分開快取，避免互相覆蓋
               .Tag("products-cache"));
});

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

// 啟用 Output Cache middleware
app.UseOutputCache();

app.UseAuthorization();

app.MapControllers();

app.Run();
