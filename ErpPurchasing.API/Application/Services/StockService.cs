using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Interfaces;

namespace ErpPurchasing.API.Application.Services;

// Şimdilik ince bir katman. Kullanıcının sadece yetkili olduğu depoları görmesi gibi
// kurallar eklenecek yer burası (Bölüm 5).
public sealed class StockService : IStockService
{
    private readonly IStockRepository _repository;

    public StockService(IStockRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<StockDto>> GetCurrentStockAsync(int? itemId, int? warehouseId)
        => _repository.GetCurrentStockAsync(itemId, warehouseId);
}