using System.Data;
using Dapper.Contrib.Extensions;
using FairwayFinder.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FairwayFinder.Core.Repositories;


public interface IBaseRepository {
    Task<int> Insert<T>(T? data) where T: class;
    Task<bool> Update<T>(T? data) where T: class;
}

public class BasePgRepository(IConfiguration configuration, ILogger logger) : IBaseRepository {
    
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger _logger = logger;
    private readonly string? _connection_string = configuration.GetConnectionString(ApplicationSettings.SQL_CONNECTION_NAME);

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

        // if (data is IDataModel data_as_datamodel) {
        //     data_as_datamodel.updated_by = _username_retriever.Username;
        //     data_as_datamodel.updated_on = DateTime.UtcNow;
        // }
        var rv = await conn.UpdateAsync(data);
        return rv;
    }

    protected async Task<NpgsqlConnection> GetNewOpenConnection() {
        var sqlConnection = new NpgsqlConnection(_connection_string);
        if (sqlConnection.State != ConnectionState.Open)
        {
            await sqlConnection.OpenAsync();
        }
        return sqlConnection;
    }
}