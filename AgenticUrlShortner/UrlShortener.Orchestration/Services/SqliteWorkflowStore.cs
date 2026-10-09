using System.Text.Json;
using Microsoft.Data.Sqlite;
using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

/// <summary>
/// SQLite-backed workflow store. Workflows survive server restarts.
/// Uses a single JSON column so no schema migration is needed when WorkflowContext evolves.
/// </summary>
public sealed class SqliteWorkflowStore : IWorkflowStore
{
    private readonly string _connectionString;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public SqliteWorkflowStore(string connectionString)
    {
        _connectionString = connectionString;
        EnsureTable();
    }

    public void Save(WorkflowContext context)
    {
        var json = JsonSerializer.Serialize(context, JsonOptions);

        using var conn = Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Workflows (WorkflowId, UpdatedAtUtc, Payload)
            VALUES ($id, $ts, $json)
            ON CONFLICT(WorkflowId) DO UPDATE SET
                UpdatedAtUtc = excluded.UpdatedAtUtc,
                Payload      = excluded.Payload;
            """;
        cmd.Parameters.AddWithValue("$id",   context.WorkflowId.ToString());
        cmd.Parameters.AddWithValue("$ts",   DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
        cmd.ExecuteNonQuery();
    }

    public WorkflowContext? Get(Guid workflowId)
    {
        using var conn = Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT Payload FROM Workflows WHERE WorkflowId = $id;";
        cmd.Parameters.AddWithValue("$id", workflowId.ToString());

        var payload = cmd.ExecuteScalar() as string;
        return payload is null
            ? null
            : JsonSerializer.Deserialize<WorkflowContext>(payload, JsonOptions);
    }

    private void EnsureTable()
    {
        using var conn = Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Workflows (
                WorkflowId   TEXT PRIMARY KEY,
                UpdatedAtUtc TEXT NOT NULL,
                Payload      TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
