using System.IO;
using Microsoft.Data.Sqlite;

namespace MtePpsControl.ViewModels;

/// <summary>
/// Persistent SQLite log of every command sent, every reply received, every action,
/// and every measurement tick — for traceability across runs.
/// File lives at %APPDATA%\MtePpsControl\operational_log.sqlite.
/// </summary>
public sealed class OperationalLog : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _lock = new();

    public OperationalLog()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MtePpsControl");
        Directory.CreateDirectory(dir);
        DatabasePath = Path.Combine(dir, "operational_log.sqlite");

        _conn = new SqliteConnection($"Data Source={DatabasePath}");
        _conn.Open();
        EnsureSchema();
    }

    public string DatabasePath { get; }

    private void EnsureSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS commands (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                ts        TEXT NOT NULL,
                sent      TEXT NOT NULL,
                received  TEXT,
                status    TEXT,
                source    TEXT
            );
            CREATE TABLE IF NOT EXISTS measurements (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                ts        TEXT NOT NULL,
                u1 REAL, u2 REAL, u3 REAL,
                i1 REAL, i2 REAL, i3 REAL,
                thdu1 REAL, thdu2 REAL, thdu3 REAL,
                thdi1 REAL, thdi2 REAL, thdi3 REAL,
                phiu1 REAL, phiu2 REAL, phiu3 REAL,
                phii1 REAL, phii2 REAL, phii3 REAL,
                statu1 TEXT, statu2 TEXT, statu3 TEXT,
                stati1 TEXT, stati2 TEXT, stati3 TEXT
            );
            CREATE TABLE IF NOT EXISTS sessions (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                started   TEXT NOT NULL,
                ended     TEXT,
                version   TEXT,
                port      TEXT,
                note      TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_commands_ts     ON commands(ts);
            CREATE INDEX IF NOT EXISTS ix_measurements_ts ON measurements(ts);
        ";
        cmd.ExecuteNonQuery();
    }

    public void LogCommand(string sent, string received, string status, string source = "GUI")
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO commands (ts, sent, received, status, source) VALUES ($ts,$sent,$rcv,$st,$src)";
            cmd.Parameters.AddWithValue("$ts",   DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$sent", sent);
            cmd.Parameters.AddWithValue("$rcv",  received ?? "");
            cmd.Parameters.AddWithValue("$st",   status ?? "");
            cmd.Parameters.AddWithValue("$src",  source);
            cmd.ExecuteNonQuery();
        }
    }

    public void LogMeasurement(double[] u, double[] i, double[] thdu, double[] thdi,
                                double[] phiu, double[] phii, string[] statU, string[] statI)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO measurements
                (ts, u1,u2,u3, i1,i2,i3, thdu1,thdu2,thdu3, thdi1,thdi2,thdi3,
                 phiu1,phiu2,phiu3, phii1,phii2,phii3,
                 statu1,statu2,statu3, stati1,stati2,stati3)
                VALUES ($ts,$u1,$u2,$u3,$i1,$i2,$i3,
                        $thdu1,$thdu2,$thdu3,$thdi1,$thdi2,$thdi3,
                        $phiu1,$phiu2,$phiu3,$phii1,$phii2,$phii3,
                        $statu1,$statu2,$statu3,$stati1,$stati2,$stati3)";
            cmd.Parameters.AddWithValue("$ts",     DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$u1",     u[0]);  cmd.Parameters.AddWithValue("$u2",     u[1]);  cmd.Parameters.AddWithValue("$u3",     u[2]);
            cmd.Parameters.AddWithValue("$i1",     i[0]);  cmd.Parameters.AddWithValue("$i2",     i[1]);  cmd.Parameters.AddWithValue("$i3",     i[2]);
            cmd.Parameters.AddWithValue("$thdu1",  thdu[0]); cmd.Parameters.AddWithValue("$thdu2",  thdu[1]); cmd.Parameters.AddWithValue("$thdu3",  thdu[2]);
            cmd.Parameters.AddWithValue("$thdi1",  thdi[0]); cmd.Parameters.AddWithValue("$thdi2",  thdi[1]); cmd.Parameters.AddWithValue("$thdi3",  thdi[2]);
            cmd.Parameters.AddWithValue("$phiu1",  phiu[0]); cmd.Parameters.AddWithValue("$phiu2",  phiu[1]); cmd.Parameters.AddWithValue("$phiu3",  phiu[2]);
            cmd.Parameters.AddWithValue("$phii1",  phii[0]); cmd.Parameters.AddWithValue("$phii2",  phii[1]); cmd.Parameters.AddWithValue("$phii3",  phii[2]);
            cmd.Parameters.AddWithValue("$statu1", statU[0]); cmd.Parameters.AddWithValue("$statu2", statU[1]); cmd.Parameters.AddWithValue("$statu3", statU[2]);
            cmd.Parameters.AddWithValue("$stati1", statI[0]); cmd.Parameters.AddWithValue("$stati2", statI[1]); cmd.Parameters.AddWithValue("$stati3", statI[2]);
            cmd.ExecuteNonQuery();
        }
    }

    public int StartSession(string version, string port, string? note = null)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO sessions (started, version, port, note) VALUES ($ts,$ver,$port,$note); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("$ts",   DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$ver",  version);
            cmd.Parameters.AddWithValue("$port", port);
            cmd.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public void EndSession(int id)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE sessions SET ended=$ts WHERE id=$id";
            cmd.Parameters.AddWithValue("$ts", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose() => _conn.Dispose();
}
