using System.Data;
using Microsoft.Data.SqlClient;

namespace ErpPurchasing.API.Infrastructure.Data;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' bulunamadı.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
