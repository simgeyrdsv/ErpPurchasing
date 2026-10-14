using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Exceptions;
using ErpPurchasing.API.Application.Interfaces;

namespace ErpPurchasing.API.Application.Services;

public sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly IGoodsReceiptRepository _repository;

    public GoodsReceiptService(IGoodsReceiptRepository repository)
    {
        _repository = repository;
    }

    public async Task<GoodsReceiptDto> GetByIdAsync(int goodsReceiptId)
    {
        return await _repository.GetByIdAsync(goodsReceiptId)
            ?? throw new NotFoundException($"Id'si {goodsReceiptId} olan mal kabul bulunamadı.");
    }

    public async Task<GoodsReceiptDto> CreateAsync(CreateGoodsReceiptRequest request)
    {
        var receiptDate = (request.ReceiptDate ?? DateTime.Today).Date;

        if (receiptDate > DateTime.Today)
            throw new BusinessRuleException("Mal kabul tarihi ileri bir tarih olamaz.");

        if (!await _repository.IsSupplierActiveAsync(request.SupplierId))
            throw new BusinessRuleException($"Id'si {request.SupplierId} olan tedarikçi bulunamadı veya pasif.");

        var requestedWarehouses = request.Lines.Select(l => l.WarehouseId).Distinct().ToList();
        var activeWarehouses = await _repository.GetActiveWarehouseIdsAsync(requestedWarehouses);
        var invalidWarehouses = requestedWarehouses.Except(activeWarehouses).ToList();
        if (invalidWarehouses.Count > 0)
            throw new BusinessRuleException($"Şu depolar bulunamadı veya pasif: {string.Join(", ", invalidWarehouses)}");

        var newId = await _repository.CreateAsync(request, receiptDate);
        return await GetByIdAsync(newId);
    }

    public async Task<GoodsReceiptDto> ApproveAsync(int goodsReceiptId)
    {
        await _repository.ApproveAsync(goodsReceiptId);
        return await GetByIdAsync(goodsReceiptId);
    }
}