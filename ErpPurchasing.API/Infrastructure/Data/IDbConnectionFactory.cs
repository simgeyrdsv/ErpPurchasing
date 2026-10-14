using System.Data;

namespace ErpPurchasing.API.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}