using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GridTrace.Models;
using Oracle.ManagedDataAccess.Client;

namespace GridTrace.Data;

public class DeviceRepository
{
    private const string ConnectionString =
        "User Id=system;Password=OraPassword1;Data Source=localhost:1521/XEPDB1;";

    public async Task<List<NetworkDevice>> GetAllDevicesAsync()
    {
        var devices = new List<NetworkDevice>();
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = new OracleCommand(
            "SELECT id, name, device_type, parent_id, pos_x, pos_y, status FROM network_devices ORDER BY id",
            connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            devices.Add(new NetworkDevice
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DeviceType = reader.GetString(2),
                ParentId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                PosX = reader.GetDouble(4),
                PosY = reader.GetDouble(5),
                Status = reader.GetString(6)
            });
        }
        return devices;
    }

    public async Task<List<int>> GetDownstreamTraceIdsAsync(int deviceId)
    {
        var ids = new List<int>();
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = new OracleCommand(
            "SELECT id FROM network_devices START WITH id = :deviceId CONNECT BY PRIOR id = parent_id",
            connection);
        command.Parameters.Add(new OracleParameter("deviceId", deviceId));
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt32(0));
        }
        return ids;
    }

    public async Task SetStatusAsync(IEnumerable<int> deviceIds, string status)
    {
        var idList = deviceIds.ToList();
        if (idList.Count == 0) return;

        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        var inClause = string.Join(",", idList.Select((_, i) => $":id{i}"));
        using var command = new OracleCommand(
            $"UPDATE network_devices SET status = :status WHERE id IN ({inClause})", connection);
        command.Parameters.Add(new OracleParameter("status", status));
        for (int i = 0; i < idList.Count; i++)
        {
            command.Parameters.Add(new OracleParameter($"id{i}", idList[i]));
        }
        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAllStatusAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = new OracleCommand(
            "UPDATE network_devices SET status = 'NORMAL'", connection);
        await command.ExecuteNonQueryAsync();
    }
}