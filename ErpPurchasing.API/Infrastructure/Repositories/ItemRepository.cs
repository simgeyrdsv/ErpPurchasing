using Dapper;
using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Exceptions;
using ErpPurchasing.API.Application.Interfaces;
using ErpPurchasing.API.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ErpPurchasing.API.Infrastructure.Repositories;

public sealed class ItemRepository : IItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ItemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectItemSql = """
        SELECT  i.ItemId, i.ItemCode, i.ItemName,
                i.ItemTypeId, it.ItemTypeName,
                u.UnitCode, i.CriticalStockLevel,
                i.IsActive, i.CreatedAt
        FROM dbo.Item i
        JOIN dbo.ItemType it ON it.ItemTypeId = i.ItemTypeId
        JOIN dbo.Unit u      ON u.UnitId      = i.UnitId
        """;

    public async Task<IEnumerable<ItemDto>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<ItemDto>($"{SelectItemSql} ORDER BY i.ItemCode;");
    }

    public async Task<ItemDto?> GetByIdAsync(int itemId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ItemDto>(
            $"{SelectItemSql} WHERE i.ItemId = @ItemId;", new { ItemId = itemId });
    }

    public async Task<bool> UnitExistsAsync(int unitId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Unit WHERE UnitId = @UnitId) THEN 1 ELSE 0 END;",
            new { UnitId = unitId });
    }

    public async Task<int> CreateAsync(CreateItemRequest request)
    {
        const string sql = """
            INSERT INTO dbo.Item (ItemCode, ItemName, ItemTypeId, UnitId, CriticalStockLevel)
            OUTPUT INSERTED.ItemId
            VALUES (@ItemCode, @ItemName, @ItemTypeId, @UnitId, @CriticalStockLevel);
            """;

        using var connection = _connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteScalarAsync<int>(sql, request);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)   // UNIQUE ihlali
        {
            throw new ConflictException($"'{request.ItemCode}' kodlu bir ürün zaten mevcut.");
        }
    }
}
