using System.Data;
using Dapper;
using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Interfaces;
using ErpPurchasing.API.Infrastructure.Data;

namespace ErpPurchasing.API.Infrastructure.Repositories;

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PurchaseOrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> IsSupplierActiveAsync(int supplierId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Supplier WHERE SupplierId = @SupplierId AND IsActive = 1) THEN 1 ELSE 0 END;",
            new { SupplierId = supplierId });
    }

    public async Task<IReadOnlyCollection<int>> GetActiveItemIdsAsync(IEnumerable<int> itemIds)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<int>(
            "SELECT ItemId FROM dbo.Item WHERE ItemId IN @Ids AND IsActive = 1;",
            new { Ids = itemIds.Distinct() });   // Dapper listeyi IN (@Ids1, @Ids2...) şeklinde açar
        return result.ToList();
    }

    public async Task<int> CreateAsync(CreatePurchaseOrderRequest request, DateTime orderDate)
    {
        const string insertHeaderSql = """
            DECLARE @Seq INT = NEXT VALUE FOR dbo.PurchaseOrderNumberSeq;
            INSERT INTO dbo.PurchaseOrder (OrderNumber, SupplierId, OrderDate, StatusId, Description)
            OUTPUT INSERTED.PurchaseOrderId
            VALUES (CONCAT('PO-', YEAR(@OrderDate), '-', FORMAT(@Seq, '000000')),
                    @SupplierId, @OrderDate, 1, @Description);
            """;

        const string insertLineSql = """
            INSERT INTO dbo.PurchaseOrderLine
                (PurchaseOrderId, LineNumber, ItemId, Quantity, UnitPrice, ExpectedDeliveryDate)
            VALUES
                (@PurchaseOrderId, @LineNumber, @ItemId, @Quantity, @UnitPrice, @ExpectedDeliveryDate);
            """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1) Başlık
            var purchaseOrderId = await connection.ExecuteScalarAsync<int>(insertHeaderSql, new
            {
                request.SupplierId,
                OrderDate = orderDate,
                request.Description
            }, transaction);

            // 2) Satırlar (satır numaraları 1'den başlayarak otomatik veriliyor)
            var lines = request.Lines.Select((line, index) => new
            {
                PurchaseOrderId = purchaseOrderId,
                LineNumber = (short)(index + 1),
                line.ItemId,
                line.Quantity,
                line.UnitPrice,
                line.ExpectedDeliveryDate
            });
            await connection.ExecuteAsync(insertLineSql, lines, transaction);

            transaction.Commit();      // Hepsi başarılı → kalıcı yap
            return purchaseOrderId;
        }
        catch
        {
            transaction.Rollback();    // Herhangi bir hata → hiçbir şey kaydedilmez
            throw;
        }
    }

    public async Task<PurchaseOrderDto?> GetByIdAsync(int purchaseOrderId)
    {
        const string sql = """
            -- 1) Başlık (toplam tutar satırlardan hesaplanıyor)
            SELECT  po.PurchaseOrderId, po.OrderNumber, po.SupplierId, s.SupplierName,
                    po.OrderDate, po.StatusId, st.StatusName, po.Description, po.CreatedAt,
                    ISNULL((SELECT SUM(l.Quantity * l.UnitPrice)
                            FROM dbo.PurchaseOrderLine l
                            WHERE l.PurchaseOrderId = po.PurchaseOrderId), 0) AS TotalAmount
            FROM dbo.PurchaseOrder po
            JOIN dbo.Supplier s             ON s.SupplierId = po.SupplierId
            JOIN dbo.PurchaseOrderStatus st ON st.StatusId  = po.StatusId
            WHERE po.PurchaseOrderId = @Id;

            -- 2) Satırlar (teslim alınan = sadece ONAYLI mal kabuller)
            SELECT  pol.PurchaseOrderLineId, pol.LineNumber, pol.ItemId,
                    i.ItemCode, i.ItemName, u.UnitCode,
                    pol.Quantity, pol.UnitPrice,
                    pol.Quantity * pol.UnitPrice                AS LineAmount,
                    pol.ExpectedDeliveryDate,
                    ISNULL(r.ReceivedQty, 0)                    AS ReceivedQuantity,
                    pol.Quantity - ISNULL(r.ReceivedQty, 0)     AS RemainingQuantity
            FROM dbo.PurchaseOrderLine pol
            JOIN dbo.Item i ON i.ItemId = pol.ItemId
            JOIN dbo.Unit u ON u.UnitId = i.UnitId
            OUTER APPLY (
                SELECT SUM(grl.Quantity) AS ReceivedQty
                FROM dbo.GoodsReceiptLine grl
                JOIN dbo.GoodsReceipt gr ON gr.GoodsReceiptId = grl.GoodsReceiptId
                WHERE grl.PurchaseOrderLineId = pol.PurchaseOrderLineId
                  AND gr.StatusId = 2
            ) r
            WHERE pol.PurchaseOrderId = @Id
            ORDER BY pol.LineNumber;
            """;

        using var connection = _connectionFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(sql, new { Id = purchaseOrderId });

        var header = await multi.ReadSingleOrDefaultAsync<PurchaseOrderDto>();
        if (header is null) return null;

        header.Lines.AddRange(await multi.ReadAsync<PurchaseOrderLineDto>());
        return header;
    }
}
