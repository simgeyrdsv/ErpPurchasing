using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

public interface IGoodsReceiptValidator
{
    void Validate(int supplierId,
                  IReadOnlyCollection<ReceiptLineInput> lines,
                  IReadOnlyDictionary<int, OrderLineSnapshot> orderLines);
}