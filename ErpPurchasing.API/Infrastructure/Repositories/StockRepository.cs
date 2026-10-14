using Dapper;
using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Interfaces;
using ErpPurchasing.API.Infrastructure.Data;

namespace ErpPurchasing.API.Infrastructure.Repositories;

public sealed class StockRepository : IStockRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<StockDto>> GetCurrentStockAsync(int? itemId, int? warehouseId)
    {
        const string sql = """
            SELECT  i.ItemId, i.ItemCode, i.ItemName, u.UnitCode,
                    w.WarehouseId, w.WarehouseCode, w.WarehouseName,
                    SUM(st.Quantity) AS Quantity
            FROM dbo.StockTransaction st
            JOIN dbo.Item i      ON i.ItemId      = st.ItemId
            JOIN dbo.Unit u      ON u.UnitId      = i.UnitId
            JOIN dbo.Warehouse w ON w.WarehouseId = st.WarehouseId
            WHERE (@ItemId IS NULL OR st.ItemId = @ItemId)
              AND (@WarehouseId IS NULL OR st.WarehouseId = @WarehouseId)
            GROUP BY i.ItemId, i.ItemCode, i.ItemName, u.UnitCode,
                     w.WarehouseId, w.WarehouseCode, w.WarehouseName
            HAVING SUM(st.Quantity) <> 0
            ORDER BY i.ItemCode, w.WarehouseCode
            OPTION (RECOMPILE);
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<StockDto>(sql, new { ItemId = itemId, WarehouseId = warehouseId });
    }
}