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
               .Tag("products-cache"));
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
