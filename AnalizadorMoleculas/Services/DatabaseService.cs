using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using AnalizadorMoleculas.Models;

namespace AnalizadorMoleculas.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public DatabaseService()
        {
            string carpetaApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnalizadorMoleculas");
            if (!Directory.Exists(carpetaApp))
            {
                Directory.CreateDirectory(carpetaApp);
            }

            _dbPath = Path.Combine(carpetaApp, "AuditoriaLocal.db");
            _connectionString = $"Data Source={_dbPath};Version=3;";
            InicializarBaseDatos();
        }
        private static string ComputeQuickHash(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                var fi = new FileInfo(filePath);
                // quick hash: filename + filesize
                return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fi.Name + "|" + fi.Length));
            }
            catch { return null; }
        }
        
        private void InicializarBaseDatos()
        {
            if (!File.Exists(_dbPath))
            {
                SQLiteConnection.CreateFile(_dbPath);
            }

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn)) pragma.ExecuteNonQuery();

                string queryHistorial = @"
                    CREATE TABLE IF NOT EXISTS HistorialAuditorias (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FechaRegistro TEXT NOT NULL,
                        NombreArchivo TEXT NOT NULL,
                        TotalRegistros INTEGER NOT NULL,
                        Hash TEXT,
                        Observaciones TEXT
                    );";

                string queryFormuladas = @"
                    CREATE TABLE IF NOT EXISTS DatosFormuladas (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        AuditoriaId INTEGER NOT NULL,
                        ATC TEXT,
                        Laboratorio TEXT,
                        Producto TEXT,
                        Presentacion TEXT,
                        Unidades INTEGER,
                        FOREIGN KEY(AuditoriaId) REFERENCES HistorialAuditorias(Id) ON DELETE CASCADE
                    );";

                string queryVentas = @"
                    CREATE TABLE IF NOT EXISTS DatosVentas (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        AuditoriaId INTEGER NOT NULL,
                        ATC TEXT,
                        Laboratorio TEXT,
                        Producto TEXT,
                        Presentacion TEXT,
                        Unidades INTEGER,
                        Valor REAL,
                        FOREIGN KEY(AuditoriaId) REFERENCES HistorialAuditorias(Id) ON DELETE CASCADE
                    );";

                using (var cmd = new SQLiteCommand(queryHistorial, conn)) cmd.ExecuteNonQuery();
                using (var idxHist = new SQLiteCommand("CREATE INDEX IF NOT EXISTS idx_hist_nombre ON HistorialAuditorias(NombreArchivo);", conn)) idxHist.ExecuteNonQuery();
                
                try
                {
                    bool hasHash = false;
                    using (var ti = new SQLiteCommand("PRAGMA table_info('HistorialAuditorias');", conn))
                    using (var reader = ti.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var colName = reader["name"]?.ToString();
                            if (string.Equals(colName, "Hash", StringComparison.OrdinalIgnoreCase))
                            {
                                hasHash = true;
                                break;
                            }
                        }
                    }

                    if (!hasHash)
                    {
                        using (var alt = new SQLiteCommand("ALTER TABLE HistorialAuditorias ADD COLUMN Hash TEXT;", conn))
                            alt.ExecuteNonQuery();
                    }

                    using (var idxHash = new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS idx_hist_hash ON HistorialAuditorias(Hash);", conn)) idxHash.ExecuteNonQuery();
                }
                catch
                {
                    // ignore migration failures but continue; index creation may fail on very old DBs
                }

                using (var cmd = new SQLiteCommand(queryFormuladas, conn)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand(queryVentas, conn)) cmd.ExecuteNonQuery();

            }
        }
        public async Task GuardarAuditoriaCompletaAsync(string nombreArchivo, int totalReg, string obs, DataTable hojaFormuladas, DataTable hojaVentas)
        {
            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // compute quick hash early and check for existing audit to avoid duplicate headers
                            string quickHash = null;
                            try { quickHash = ComputeQuickHash(nombreArchivo); } catch { quickHash = null; }

                            if (!string.IsNullOrEmpty(quickHash))
                            {
                                using (var chk = new SQLiteCommand("SELECT Id FROM HistorialAuditorias WHERE Hash = @hash LIMIT 1", conn, trans))
                                {
                                    chk.Parameters.AddWithValue("@hash", quickHash);
                                    var existing = chk.ExecuteScalar();
                                    if (existing != null)
                                    {
                                        // already imported; update TotalRegistros from actual counts
                                        int existingId = Convert.ToInt32(existing);
                                        using (var cnt = new SQLiteCommand(@"SELECT 
                                                (SELECT COUNT(*) FROM DatosFormuladas WHERE AuditoriaId = @id) + 
                                                (SELECT COUNT(*) FROM DatosVentas WHERE AuditoriaId = @id)
                                                as Total", conn, trans))
                                        {
                                            cnt.Parameters.AddWithValue("@id", existingId);
                                            var totalObj = cnt.ExecuteScalar();
                                            int total = totalObj != null ? Convert.ToInt32(totalObj) : 0;
                                            using (var upd = new SQLiteCommand("UPDATE HistorialAuditorias SET TotalRegistros = @total WHERE Id = @id", conn, trans))
                                            {
                                                upd.Parameters.AddWithValue("@total", total);
                                                upd.Parameters.AddWithValue("@id", existingId);
                                                upd.ExecuteNonQuery();
                                            }
                                        }

                                        trans.Commit();
                                        return;
                                    }
                                }
                            }

                            long auditoriaId = 0;
                            string queryInsertCabecera = @"
                                INSERT INTO HistorialAuditorias (FechaRegistro, NombreArchivo, TotalRegistros, Hash, Observaciones)
                                VALUES (@fecha, @nombre, @total, @hash, @obs);
                                SELECT last_insert_rowid();";

                            using (var cmd = new SQLiteCommand(queryInsertCabecera, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@nombre", nombreArchivo);
                                cmd.Parameters.AddWithValue("@total", totalReg);
                                cmd.Parameters.AddWithValue("@hash", quickHash ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@obs", obs ?? (object)DBNull.Value);
                                auditoriaId = (long)cmd.ExecuteScalar();
                            }

                            int insertedCount = 0;
                            bool skipIfDuplicate = false;
                            // duplicate check already done above by hash; skipIfDuplicate stays false
                            if (!skipIfDuplicate && hojaFormuladas != null && hojaFormuladas.Rows.Count > 0)
                            {
                                string queryForm = @"
                                    INSERT INTO DatosFormuladas (AuditoriaId, ATC, Laboratorio, Producto, Presentacion, Unidades)
                                    VALUES (@auditoriaId, @atc, @laboratorio, @producto, @presentacion, @unidades);";

                            using (var idxCmd = new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS idx_form_unique ON DatosFormuladas(AuditoriaId, ATC, Laboratorio, Presentacion);", conn, trans)) idxCmd.ExecuteNonQuery();

                            using (var cmdForm = new SQLiteCommand(queryForm, conn, trans))
                            {
                                    cmdForm.Parameters.Add("@auditoriaId", DbType.Int64).Value = auditoriaId;
                                    var pAtc = cmdForm.Parameters.Add("@atc", DbType.String);
                                    var pLab = cmdForm.Parameters.Add("@laboratorio", DbType.String);
                                    var pProd = cmdForm.Parameters.Add("@producto", DbType.String);
                                    var pPres = cmdForm.Parameters.Add("@presentacion", DbType.String);
                                    var pUn = cmdForm.Parameters.Add("@unidades", DbType.Int32);

                                    foreach (DataRow row in hojaFormuladas.Rows)
                                    {
                                        pAtc.Value = row.Table.Columns.Contains("ATC") ? row["ATC"] : (row.Table.Columns.Contains("Molecula") ? row["Molecula"] : DBNull.Value);
                                        pLab.Value = row.Table.Columns.Contains("Laboratorio") ? row["Laboratorio"] : (row.Table.Columns.Contains("Lab") ? row["Lab"] : DBNull.Value);
                                        pProd.Value = row.Table.Columns.Contains("Producto") ? row["Producto"] : DBNull.Value;
                                        pPres.Value = row.Table.Columns.Contains("Presentacion") ? row["Presentacion"] : (row.Table.Columns.Contains("Pack") ? row["Pack"] : DBNull.Value);
                                        pUn.Value = row.Table.Columns.Contains("Unidades") ? row["Unidades"] : 0;

                                        try
                                        {
                                            cmdForm.ExecuteNonQuery();
                                            insertedCount++;
                                        }
                                        catch (SQLiteException) { /* ignorar duplicados por índice único */ }
                                    }
                                }
                            }

                            if (!skipIfDuplicate && hojaVentas != null && hojaVentas.Rows.Count > 0)
                            {
                                string queryVentas = @"
                                    INSERT INTO DatosVentas (AuditoriaId, ATC, Laboratorio, Producto, Presentacion, Unidades, Valor)
                                    VALUES (@auditoriaId, @atc, @laboratorio, @producto, @presentacion, @unidades, @valor);";

                                using (var idxCmd2 = new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS idx_vent_unique ON DatosVentas(AuditoriaId, ATC, Laboratorio, Presentacion);", conn, trans)) idxCmd2.ExecuteNonQuery();

                                using (var cmdVentas = new SQLiteCommand(queryVentas, conn, trans))
                                {
                                    cmdVentas.Parameters.Add("@auditoriaId", DbType.Int64).Value = auditoriaId;
                                    var pAtc = cmdVentas.Parameters.Add("@atc", DbType.String);
                                    var pLab = cmdVentas.Parameters.Add("@laboratorio", DbType.String);
                                    var pProd = cmdVentas.Parameters.Add("@producto", DbType.String);
                                    var pPres = cmdVentas.Parameters.Add("@presentacion", DbType.String);
                                    var pUn = cmdVentas.Parameters.Add("@unidades", DbType.Int32);
                                    var pVal = cmdVentas.Parameters.Add("@valor", DbType.Double);

                                    foreach (DataRow row in hojaVentas.Rows)
                                    {
                                        pAtc.Value = row.Table.Columns.Contains("ATC") ? row["ATC"] : (row.Table.Columns.Contains("Molecula") ? row["Molecula"] : DBNull.Value);
                                        pLab.Value = row.Table.Columns.Contains("Laboratorio") ? row["Laboratorio"] : (row.Table.Columns.Contains("Lab") ? row["Lab"] : DBNull.Value);
                                        pProd.Value = row.Table.Columns.Contains("Producto") ? row["Producto"] : DBNull.Value;
                                        pPres.Value = row.Table.Columns.Contains("Presentacion") ? row["Presentacion"] : (row.Table.Columns.Contains("Pack") ? row["Pack"] : DBNull.Value);
                                        pUn.Value = row.Table.Columns.Contains("Unidades") ? row["Unidades"] : 0;
                                        pVal.Value = row.Table.Columns.Contains("Valor") ? row["Valor"] : 0.0;

                                        try
                                        {
                                            cmdVentas.ExecuteNonQuery();
                                            insertedCount++;
                                        }
                                        catch (SQLiteException) { /* ignorar duplicados por índice único */ }
                                    }
                                }
                            }
                            using (var upd = new SQLiteCommand("UPDATE HistorialAuditorias SET TotalRegistros = @total WHERE Id = @id", conn, trans))
                            {
                                upd.Parameters.AddWithValue("@total", insertedCount);
                                upd.Parameters.AddWithValue("@id", auditoriaId);
                                upd.ExecuteNonQuery();
                            }

                            trans.Commit();
                        }
                        catch (Exception ex)
                        {
                            trans.Rollback();
                            throw new Exception("Error crítico al guardar la auditoría masiva: " + ex.Message);
                        }
                    }
                }
            });
        }

        public async Task VerificarBaseDeDatosAsync()
        {
            try
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM HistorialAuditorias", conn))
                    {
                        int total = Convert.ToInt32(cmd.ExecuteScalar());
                        Console.WriteLine($"📊 Total de auditorías guardadas: {total}");
                    }

                    string query = @"
                        SELECT Id, FechaRegistro, NombreArchivo, TotalRegistros
                        FROM HistorialAuditorias 
                        ORDER BY Id DESC LIMIT 5;";

                    using (var cmd = new SQLiteCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"ID: {reader["Id"]} | Fecha: {reader["FechaRegistro"]} | Archivo: {reader["NombreArchivo"]} | Registros: {reader["TotalRegistros"]}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error al leer BD: " + ex.Message);
            }
        }

        public async Task<List<AuditoriaHistorial>> ObtenerHistorialAsync()
        {
            return await Task.Run(() =>
            {
                var lista = new List<AuditoriaHistorial>();
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    string selectQuery = "SELECT Id, FechaRegistro, NombreArchivo, TotalRegistros, Observaciones FROM HistorialAuditorias ORDER BY FechaRegistro DESC;";
                    using (var cmd = new SQLiteCommand(selectQuery, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new AuditoriaHistorial
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                FechaRegistro = Convert.ToDateTime(reader["FechaRegistro"]),
                                NombreArchivo = reader["NombreArchivo"].ToString(),
                                TotalRegistros = Convert.ToInt32(reader["TotalRegistros"]),
                                Observaciones = reader["Observaciones"].ToString()
                            });
                        }
                    }
                }
                return lista;
            });
        }

        public async Task EliminarAuditoriaAsync(int id)
        {
            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SQLiteCommand("DELETE FROM HistorialAuditorias WHERE Id = @id", conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@id", id);
                                cmd.ExecuteNonQuery();
                            }
                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }
            });
        }
    }
}