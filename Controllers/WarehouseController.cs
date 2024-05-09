namespace Zadanie4APBD.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

    [ApiController]
    [Route("[controller]")]
    public class WarehouseController : ControllerBase
    {
        private readonly string _connectionString;

        public WarehouseController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpPost("add-product")]
        public async Task<IActionResult> AddProduct([FromBody] AddProductRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than 0.");

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var productExists = await CheckIfExistsAsync(connection, "Product", "IdProduct", request.IdProduct);
                if (!productExists)
                    return NotFound("Product not found.");

                var warehouseExists = await CheckIfExistsAsync(connection, "Warehouse", "IdWarehouse", request.IdWarehouse);
                if (!warehouseExists)
                    return NotFound("Warehouse not found.");

                var orderExists = await CheckOrderAsync(connection, request);
                if (!orderExists)
                    return NotFound("Order not found or already fulfilled.");

                var newProductWarehouseId = await AddProductToWarehouseAsync(connection, request);
                return Ok(new { IdProductWarehouse = newProductWarehouseId });
            }
        }

        [HttpPost("add-product-proc")]
        public async Task<IActionResult> AddProductUsingProc([FromBody] AddProductRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than 0.");

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("AddProductToWarehouse", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                    command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                    command.Parameters.AddWithValue("@Amount", request.Amount);
                    command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

                    try
                    {
                        var newId = (int)await command.ExecuteScalarAsync();
                        return Ok(new { IdProductWarehouse = newId });
                    }
                    catch (SqlException ex)
                    {
                        return BadRequest(ex.Message);
                    }
                }
            }
        }

        private async Task<bool> CheckIfExistsAsync(SqlConnection connection, string tableName, string columnName, int id)
        {
            var query = $"SELECT COUNT(1) FROM {tableName} WHERE {columnName} = @Id";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                return (int)await command.ExecuteScalarAsync() > 0;
            }
        }

        private async Task<bool> CheckOrderAsync(SqlConnection connection, AddProductRequest request)
        {
            var query = @"SELECT COUNT(1) 
                          FROM [Order] o 
                          LEFT JOIN Product_Warehouse pw ON o.IdOrder = pw.IdOrder 
                          WHERE o.IdProduct = @IdProduct 
                          AND o.Amount = @Amount 
                          AND pw.IdProductWarehouse IS NULL 
                          AND o.CreatedAt < @CreatedAt";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                command.Parameters.AddWithValue("@Amount", request.Amount);
                command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
                return (int)await command.ExecuteScalarAsync() > 0;
            }
        }

        private async Task<int> AddProductToWarehouseAsync(SqlConnection connection, AddProductRequest request)
        {
            var updateOrderQuery = "UPDATE [Order] SET FulfilledAt = @FulfilledAt WHERE IdOrder = @IdOrder";
            var insertProductWarehouseQuery = @"INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt) 
                                                VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt); 
                                                SELECT CAST(scope_identity() AS int)";

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var updateCommand = new SqlCommand(updateOrderQuery, connection, transaction))
                    {
                        updateCommand.Parameters.AddWithValue("@FulfilledAt", request.CreatedAt);
                        updateCommand.Parameters.AddWithValue("@IdOrder", request.IdOrder);
                        await updateCommand.ExecuteNonQueryAsync();
                    }

                    using (var insertCommand = new SqlCommand(insertProductWarehouseQuery, connection, transaction))
                    {
                        insertCommand.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
                        insertCommand.Parameters.AddWithValue("@IdProduct", request.IdProduct);
                        insertCommand.Parameters.AddWithValue("@IdOrder", request.IdOrder);
                        insertCommand.Parameters.AddWithValue("@Amount", request.Amount);
                        insertCommand.Parameters.AddWithValue("@Price", request.Amount * request.Price);
                        insertCommand.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);
                        var newId = (int)await insertCommand.ExecuteScalarAsync();
                        transaction.Commit();
                        return newId;
                    }
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    public class AddProductRequest
    {
        public int IdProduct { get; set; }
        public int IdWarehouse { get; set; }
        public int Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal Price { get; set; }
        public int IdOrder { get; set; }
    }


