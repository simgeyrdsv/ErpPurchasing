using ErpPurchasing.API.Infrastructure.Data;
using ErpPurchasing.API.Middleware;
using ErpPurchasing.API.Application.Interfaces;
using ErpPurchasing.API.Application.Services;
using ErpPurchasing.API.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ---------- Servis kayıtları (DI container) ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Veritabanı bağlantısı
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
// Ürünler
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IItemService, ItemService>();
// Satınalma siparişleri
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
// Mal kabul
builder.Services.AddSingleton<IGoodsReceiptValidator, GoodsReceiptValidator>();   // Durumsuz → tek örnek yeterli
builder.Services.AddScoped<IGoodsReceiptRepository, GoodsReceiptRepository>();
builder.Services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
// Stok
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IStockService, StockService>();

// ---------- HTTP pipeline ----------
var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();   // EN BAŞTA olmalı

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();