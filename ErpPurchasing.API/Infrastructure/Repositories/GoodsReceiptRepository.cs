using System.Data;
using Dapper;
using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Exceptions;
using ErpPurchasing.API.Application.Interfaces;
using ErpPurchasing.API.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ErpPurchasing.API.Infrastructure.Repositories;

public sealed class GoodsReceiptRepository : IGoodsReceiptRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IGoodsReceiptValidator _validator;

    public GoodsReceiptRepository(IDbConnectionFactory connectionFactory, IGoodsReceiptValidator validator)
    {
        _connectionFactory = connectionFactory;
        _validator = validator;
    }

    // ---------------------------------------------------------------- Basit kontroller
    public async Task<bool> IsSupplierActiveAsync(int supplierId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Supplier WHERE SupplierId = @Id AND IsActive = 1) THEN 1 ELSE 0 END;",
            new { Id = supplierId });
    }

    public async Task<IReadOnlyCollection<int>> GetActiveWarehouseIdsAsync(IEnumerable<int> warehouseIds)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<int>(
            "SELECT WarehouseId FROM dbo.Warehouse WHERE WarehouseId IN @Ids AND IsActive = 1;",
            new { Ids = warehouseIds.Distinct() });
        return result.ToList();
    }

    // ---------------------------------------------------------------- Oluştur (+ isteğe bağlı onay)
    public async Task<int> CreateAsync(CreateGoodsReceiptRequest request, DateTime receiptDate)
    {
        const string insertHeaderSql = """
            DECLARE @Seq INT = NEXT VALUE FOR dbo.GoodsReceiptNumberSeq;
            INSERT INTO dbo.GoodsReceipt (ReceiptNumber, SupplierId, DeliveryNoteNumber, ReceiptDate, StatusId, Description)
            OUTPUT INSERTED.GoodsReceiptId
            VALUES (CONCAT('GR-', YEAR(@ReceiptDate), '-', FORMAT(@Seq, '000000')),
                    @SupplierId, @DeliveryNoteNumber, @ReceiptDate, 1, @Description);
            """;

        const string insertLineSql = """
            INSERT INTO dbo.GoodsReceiptLine (GoodsReceiptId, LineNumber, PurchaseOrderLineId, WarehouseId, Quantity)
            VALUES (@GoodsReceiptId, @LineNumber, @PurchaseOrderLineId, @WarehouseId, @Quantity);
            """;

        var inputs = request.Lines
            .Select(l => new ReceiptLineInput(l.PurchaseOrderLineId, l.WarehouseId, l.Quantity))
            .ToList();

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 0) Önce en anlamlı kontrol: Bu irsaliye daha önce girilmiş mi?
            var duplicate = await connection.ExecuteScalarAsync<bool>(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.GoodsReceipt WITH (UPDLOCK, HOLDLOCK)
                    WHERE SupplierId = @SupplierId AND DeliveryNoteNumber = @DeliveryNoteNumber
                ) THEN 1 ELSE 0 END;
                """,
                new { request.SupplierId, request.DeliveryNoteNumber }, transaction);

            if (duplicate)
                throw new ConflictException(
                    $"'{request.DeliveryNoteNumber}' numaralı irsaliye bu tedarikçi için daha önce girilmiş.");
            // 1-3) Kilitle → oku → doğrula
            var snapshots = await LockAndLoadOrderLinesAsync(connection, transaction, inputs);
            _validator.Validate(request.SupplierId, inputs, snapshots);

            // 4) Başlık (aynı irsaliye ikinci kez girilemez)
            int goodsReceiptId;
            try
            {
                goodsReceiptId = await connection.ExecuteScalarAsync<int>(insertHeaderSql, new
                {
                    request.SupplierId,
                    request.DeliveryNoteNumber,
                    ReceiptDate = receiptDate,
                    request.Description
                }, transaction);
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            {
                throw new ConflictException(
                    $"'{request.DeliveryNoteNumber}' numaralı irsaliye bu tedarikçi için daha önce girilmiş.");
            }

            // 4) Satırlar
            await connection.ExecuteAsync(insertLineSql, inputs.Select((l, i) => new
            {
                GoodsReceiptId = goodsReceiptId,
                LineNumber = (short)(i + 1),
                l.PurchaseOrderLineId,
                l.WarehouseId,
                l.Quantity
            }), transaction);

            // 5) Onay istenmişse: stoğa işle
            if (request.Approve)
                await PostApprovalAsync(connection, transaction, goodsReceiptId);

            transaction.Commit();
            return goodsReceiptId;
        }
        catch
        {
            if (transaction.Connection is not null) transaction.Rollback();
            throw;
        }
    }

    // ---------------------------------------------------------------- Taslağı onayla
    public async Task ApproveAsync(int goodsReceiptId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Başlığı kilitle: Aynı belge aynı anda iki kez onaylanamasın
            var header = await connection.QuerySingleOrDefaultAsync<ReceiptHeaderState>(
                "SELECT SupplierId, StatusId FROM dbo.GoodsReceipt WITH (UPDLOCK, ROWLOCK) WHERE GoodsReceiptId = @Id;",
                new { Id = goodsReceiptId }, transaction)
                ?? throw new NotFoundException($"Id'si {goodsReceiptId} olan mal kabul bulunamadı.");

            if (header.StatusId == 2)
                throw new ConflictException("Bu mal kabul zaten onaylanmış.");

            var inputs = (await connection.QueryAsync<ReceiptLineInput>(
                "SELECT PurchaseOrderLineId, WarehouseId, Quantity FROM dbo.GoodsReceiptLine WHERE GoodsReceiptId = @Id;",
                new { Id = goodsReceiptId }, transaction)).ToList();

            var snapshots = await LockAndLoadOrderLinesAsync(connection, transaction, inputs);
            _validator.Validate(header.SupplierId, inputs, snapshots);

            await PostApprovalAsync(connection, transaction, goodsReceiptId);

            transaction.Commit();
        }
        catch
        {
            if (transaction.Connection is not null) transaction.Rollback();
            throw;
        }
    }

    // ---------------------------------------------------------------- Getir
    public async Task<GoodsReceiptDto?> GetByIdAsync(int goodsReceiptId)
    {
        const string sql = """
            SELECT  gr.GoodsReceiptId, gr.ReceiptNumber, gr.SupplierId, s.SupplierName,
                    gr.DeliveryNoteNumber, gr.ReceiptDate, gr.StatusId, st.StatusName,
                    gr.ApprovedAt, gr.Description, gr.CreatedAt
            FROM dbo.GoodsReceipt gr
            JOIN dbo.Supplier s            ON s.SupplierId = gr.SupplierId
            JOIN dbo.GoodsReceiptStatus st ON st.StatusId  = gr.StatusId
            WHERE gr.GoodsReceiptId = @Id;

            SELECT  grl.GoodsReceiptLineId, grl.LineNumber, grl.PurchaseOrderLineId, po.OrderNumber,
                    i.ItemCode, i.ItemName, u.UnitCode, w.WarehouseCode, grl.Quantity
            FROM dbo.GoodsReceiptLine grl
            JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = grl.PurchaseOrderLineId
            JOIN dbo.PurchaseOrder po      ON po.PurchaseOrderId      = pol.PurchaseOrderId
            JOIN dbo.Item i                ON i.ItemId                = pol.ItemId
            JOIN dbo.Unit u                ON u.UnitId                = i.UnitId
            JOIN dbo.Warehouse w           ON w.WarehouseId           = grl.WarehouseId
            WHERE grl.GoodsReceiptId = @Id
            ORDER BY grl.LineNumber;
            """;

        using var connection = _connectionFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(sql, new { Id = goodsReceiptId });

        var header = await multi.ReadSingleOrDefaultAsync<GoodsReceiptDto>();
        if (header is null) return null;

        header.Lines.AddRange(await multi.ReadAsync<GoodsReceiptLineDto>());
        return header;
    }

    // ================================================================ Yardımcılar

    // Sipariş satırlarını KİLİTLER, sonra güncel durumlarını okur
    private static async Task<Dictionary<int, OrderLineSnapshot>> LockAndLoadOrderLinesAsync(
        IDbConnection connection, IDbTransaction transaction, IEnumerable<ReceiptLineInput> inputs)
    {
        var ids = inputs.Select(i => i.PurchaseOrderLineId).Distinct().ToList();

        // Adım A: Kilidi al. Başka bir işlem aynı satırları kilitlediyse burada BEKLER.
        await connection.ExecuteAsync(
            "SELECT PurchaseOrderLineId FROM dbo.PurchaseOrderLine WITH (UPDLOCK, ROWLOCK) WHERE PurchaseOrderLineId IN @Ids;",
            new { Ids = ids }, transaction);

        // Adım B: Kilit bizdeyken güncel toplamları oku
        const string sql = """
            SELECT  pol.PurchaseOrderLineId, po.OrderNumber, po.SupplierId,
                    po.StatusId AS OrderStatusId, st.StatusName AS OrderStatusName,
                    i.ItemName, pol.Quantity AS OrderedQuantity,
                    ISNULL((SELECT SUM(grl.Quantity)
                            FROM dbo.GoodsReceiptLine grl
                            JOIN dbo.GoodsReceipt gr ON gr.GoodsReceiptId = grl.GoodsReceiptId
                            WHERE grl.PurchaseOrderLineId = pol.PurchaseOrderLineId
                              AND gr.StatusId = 2), 0) AS ReceivedQuantity
            FROM dbo.PurchaseOrderLine pol
            JOIN dbo.PurchaseOrder po        ON po.PurchaseOrderId = pol.PurchaseOrderId
            JOIN dbo.PurchaseOrderStatus st  ON st.StatusId        = po.StatusId
            JOIN dbo.Item i                  ON i.ItemId           = pol.ItemId
            WHERE pol.PurchaseOrderLineId IN @Ids;
            """;

        var rows = await connection.QueryAsync<OrderLineSnapshot>(sql, new { Ids = ids }, transaction);
        return rows.ToDictionary(r => r.PurchaseOrderLineId);
    }

    // Onay: durum + stok hareketleri + sipariş durumu (hepsi aynı transaction'da)
    private static async Task PostApprovalAsync(IDbConnection connection, IDbTransaction transaction, int goodsReceiptId)
    {
        const string sql = """
            -- a) Mal kabulü onayla
            UPDATE dbo.GoodsReceipt
            SET StatusId = 2, ApprovedAt = SYSDATETIME()
            WHERE GoodsReceiptId = @Id;

            -- b) Her satır için stok GİRİŞ hareketi
            INSERT INTO dbo.StockTransaction
                (TransactionTypeId, ItemId, WarehouseId, Quantity, TransactionDate, GoodsReceiptLineId, Description)
            SELECT 1, pol.ItemId, grl.WarehouseId, grl.Quantity,
                   CAST(gr.ReceiptDate AS DATETIME2(0)), grl.GoodsReceiptLineId, gr.ReceiptNumber
            FROM dbo.GoodsReceiptLine grl
            JOIN dbo.GoodsReceipt gr       ON gr.GoodsReceiptId       = grl.GoodsReceiptId
            JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = grl.PurchaseOrderLineId
            WHERE grl.GoodsReceiptId = @Id;

            -- c) Etkilenen siparişlerin durumunu güncelle: Eksik satır varsa Kısmi Teslim (2), yoksa Tamamlandı (3)
            UPDATE po
            SET StatusId = CASE WHEN EXISTS (
                    SELECT 1
                    FROM dbo.PurchaseOrderLine l
                    WHERE l.PurchaseOrderId = po.PurchaseOrderId
                      AND l.Quantity > ISNULL((SELECT SUM(x.Quantity)
                                               FROM dbo.GoodsReceiptLine x
                                               JOIN dbo.GoodsReceipt g ON g.GoodsReceiptId = x.GoodsReceiptId
                                               WHERE x.PurchaseOrderLineId = l.PurchaseOrderLineId
                                                 AND g.StatusId = 2), 0)
                ) THEN 2 ELSE 3 END
            FROM dbo.PurchaseOrder po
            WHERE po.PurchaseOrderId IN (
                SELECT pol.PurchaseOrderId
                FROM dbo.GoodsReceiptLine grl
                JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = grl.PurchaseOrderLineId
                WHERE grl.GoodsReceiptId = @Id);
            """;

        await connection.ExecuteAsync(sql, new { Id = goodsReceiptId }, transaction);
    }

    private sealed class ReceiptHeaderState
    {
        public int SupplierId { get; init; }
        public byte StatusId { get; init; }
    }
}