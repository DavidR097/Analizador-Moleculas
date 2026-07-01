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
        
        private void InicializarBaseDatos()
        {
            if (!File.Exists(_dbPath))
            {
                SQLiteConnection.CreateFile(_dbPath);
            }

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string queryHistorial = @"
                    CREATE TABLE IF NOT EXISTS HistorialAuditorias (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FechaRegistro TEXT NOT NULL,
                        NombreArchivo TEXT NOT NULL,
                        TotalRegistros INTEGER NOT NULL,                        
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
                    // Añadir índice en HistorialAuditorias para nombre de archivo y fecha para acelerar consultas y evitar duplicados cercanos
                    using (var idxHist = new SQLiteCommand("CREATE INDEX IF NOT EXISTS idx_hist_nombre ON HistorialAuditorias(NombreArchivo);", conn)) idxHist.ExecuteNonQuery();
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
                            long auditoriaId = 0;
                            string queryInsertCabecera = @"
                                INSERT INTO HistorialAuditorias (FechaRegistro, NombreArchivo, TotalRegistros, Observaciones)
                                VALUES (@fecha, @nombre, @total, @obs);
                                SELECT last_insert_rowid();";

                            // Insert header for the new auditoria (always insert). We'll update TotalRegistros after inserting details
                            using (var cmd = new SQLiteCommand(queryInsertCabecera, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@nombre", nombreArchivo);
                                cmd.Parameters.AddWithValue("@total", totalReg);
                                cmd.Parameters.AddWithValue("@obs", obs ?? (object)DBNull.Value);
                                auditoriaId = (long)cmd.ExecuteScalar();
                            }

                            int insertedCount = 0;
                            if (hojaFormuladas != null && hojaFormuladas.Rows.Count > 0)
                            {
                                string queryForm = @"
                                    INSERT INTO DatosFormuladas (AuditoriaId, ATC, Laboratorio, Producto, Presentacion, Unidades)
                                    VALUES (@auditoriaId, @atc, @laboratorio, @producto, @presentacion, @unidades);";

                                // No-op wrapper: ensure file is saved before adding duplicate protection in a later change.
                            // Add unique index to avoid duplicate rows for the same auditoria and same fila clave
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

                            if (hojaVentas != null && hojaVentas.Rows.Count > 0)
                            {
                                string queryVentas = @"
                                    INSERT INTO DatosVentas (AuditoriaId, ATC, Laboratorio, Producto, Presentacion, Unidades, Valor)
                                    VALUES (@auditoriaId, @atc, @laboratorio, @producto, @presentacion, @unidades, @valor);";

                                // Unique index for ventas
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
                            // Update header TotalRegistros with actual inserted count (formuladas + ventas)
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
    }
}