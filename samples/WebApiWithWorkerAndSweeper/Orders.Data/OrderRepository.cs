using Microsoft.Data.SqlClient;
using Orders.Domain;
using Orders.Domain.Entities;

namespace Orders.Data;

public class OrderRepository : IOrderRepository
{
    private readonly SqlUnitOfWork _sqlConnection;

    public OrderRepository(SqlUnitOfWork sqlConnection)
    {
        _sqlConnection = sqlConnection;
    }
    
    public async Task CreateOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var sql = "Insert Into dbo.[Orders] (Version, Number, Type, Status, ActionsPending) values (@Version, @Number, @Type, @Status, @ActionPending)";

        var sqlParams = new SqlParameter[]
        {
            new SqlParameter("@Version", 1),
            new SqlParameter("@Number", order.Number),
            new SqlParameter("@Type", order.Type),
            new SqlParameter("@Status", order.Status),
            new SqlParameter("@ActionPending", false)
        };
        
        var command = await _sqlConnection.CreateSqlCommandAsync(sql, sqlParams, cancellationToken);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var sql = "Update dbo.[Orders] SET [Version] = @Version, [Status] = @Status, [ActionsPending] = @ActionPending Where Id = @OrderId";

        var sqlParams = new SqlParameter[]
        {
            new SqlParameter("@Version", order.Version),
            new SqlParameter("@Status", order.Status),
            new SqlParameter("@ActionPending", false),
            new SqlParameter("@OrderId", order.Id)
        };
        
        var command = await _sqlConnection.CreateSqlCommandAsync(sql, sqlParams, cancellationToken);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    public async Task<Order> GetOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        var sql = "Select Top(1) Id, Version, Number, Type, ActionsPending, Status From dbo.[Orders] Where Id = @OrderId Order By Version desc";

        var sqlParams = new SqlParameter[]
        {
            new SqlParameter("@OrderId", orderId),
        };
        
        var command = await _sqlConnection.CreateSqlCommandAsync(sql, sqlParams, cancellationToken);

        var reader = await command.ExecuteReaderAsync(cancellationToken);

        Order order = null;
        
        if (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64(reader.GetOrdinal("Id"));
            var version = reader.GetInt32(reader.GetOrdinal("Version"));
            var number = reader.GetString(reader.GetOrdinal("Number"));
            var type = (OrderType)reader.GetInt32(reader.GetOrdinal("Type"));
            var pending = reader.GetBoolean(reader.GetOrdinal("ActionsPending"));
            var status = (OrderStatus)reader.GetInt32(reader.GetOrdinal("Status"));

            order = new Order(id, number, type, pending, status, version);
        }

        return order;
    }
}
