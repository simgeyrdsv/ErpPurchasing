using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

public interface IStockRepository
{
    Task<IEnumerable<StockDto>> GetCurrentStockAsync(int? itemId, int? warehouseId);
}
