using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

public interface IGoodsReceiptService
{
    Task<GoodsReceiptDto> CreateAsync(CreateGoodsReceiptRequest request);
    Task<GoodsReceiptDto> ApproveAsync(int goodsReceiptId);
    Task<GoodsReceiptDto> GetByIdAsync(int goodsReceiptId);
}