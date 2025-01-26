using System.Data;
using Dapper.Contrib.Extensions;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FairwayFinder.Core.Repositories;

public class BasePgRepository(IConfiguration configuration) : IBaseRepository
{
    public async Task<int> Insert<T>(T? data) where T: class {
        
        if (data == null)
        {
            return -1;
        }
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.InsertAsync(data);
        return rv;
    }
    
    public async Task<bool> Update<T>(T? data) where T: class {
        if (data == null)
        {
            return false;
        }
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.UpdateAsync(data);
        return rv;
    }

    protected async Task<NpgsqlConnection> GetNewOpenConnection() {
        var sql_connection = new NpgsqlConnection(configuration.GetConnectionString("DbConnection"));
        if (sql_connection.State != ConnectionState.Open)
        {
            await sql_connection.OpenAsync();
        }
        return sql_connection;
    }
    
    
}