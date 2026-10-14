using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

// Veri erişim sözleşmesi: SADECE veritabanı işleri
public interface IItemRepository
{
    Task<IEnumerable<ItemDto>> GetAllAsync();
    Task<ItemDto?> GetByIdAsync(int itemId);
    Task<bool> UnitExistsAsync(int unitId);
    Task<int> CreateAsync(CreateItemRequest request);
}
