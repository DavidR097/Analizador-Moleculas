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
            // SOLUCIÓN PERMISOS: Guardar de forma segura en AppData\Roaming del usuario actual
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

                // 1. Tabla Maestra del Historial
                string queryHistorial = @"
                    CREATE TABLE IF NOT EXISTS HistorialAuditorias (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FechaRegistro TEXT NOT NULL,
                        NombreArchivo TEXT NOT NULL,
                        TotalRegistros INTEGER NOT NULL,
                        CoincidenciaCanales REAL NOT NULL,
                        Observaciones TEXT
                    );";

                // 2. Tabla Detalle para Hoja Formuladas
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

                // 3. Tabla Detalle para Hoja Ventas
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
                using (var cmd = new SQLiteCommand(queryFormuladas, conn)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand(queryVentas, conn)) cmd.ExecuteNonQuery();
            }
        }

        // MÉTODO NUEVO: Guarda en bloque todo el Excel asociado a una auditoría usando transacciones rápidas
        public async Task GuardarAuditoriaCompletaAsync(string nombreArchivo, int totalReg, double coincidencia, string obs, DataTable hojaFormuladas, DataTable hojaVentas)
        {
            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    // Usamos una transacción para que inserte miles de filas en 1 o 2 segundos en lugar de minutos
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Insertar en la cabecera
                            long auditoriaId = 0;
                            string queryInsertCabecera = @"
                                INSERT INTO HistorialAuditorias (FechaRegistro, NombreArchivo, TotalRegistros, CoincidenciaCanales, Observaciones)
                                VALUES (@fecha, @nombre, @total, @coincidencia, @obs);
                                SELECT last_insert_rowid();";

                            using (var cmd = new SQLiteCommand(queryInsertCabecera, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@nombre", nombreArchivo);
                                cmd.Parameters.AddWithValue("@total", totalReg);
                                cmd.Parameters.AddWithValue("@coincidencia", coincidencia);
                                cmd.Parameters.AddWithValue("@obs", obs ?? (object)DBNull.Value);
                                auditoriaId = (long)cmd.ExecuteScalar();
                            }

                            // 2. Insertar filas de la Hoja Formuladas
                            if (hojaFormuladas != null)
                            {
                                string queryForm = "INSERT INTO DatosFormuladas (AuditoriaId, ATC, Laboratorio, Producto, Presentacion, Unidades) VALUES (@aId, @atc, @lab, @prod, @pres, @un);";
                                using (var cmdForm = new SQLiteCommand(queryForm, conn, trans))
                                {
                                    // Preparamos los parámetros reutilizables por rendimiento
                                    cmdForm.Parameters.Add("@aId", DbType.Int64).Value = auditoriaId;
                                    var pAtc = cmdForm.Parameters.Add("@atc", DbType.String);
                                    var pLab = cmdForm.Parameters.Add("@lab", DbType.String);
                                    var pProd = cmdForm.Parameters.Add("@prod", DbType.String);
                                    var pPres = cmdForm.Parameters.Add("@pres", DbType.String);
                                    var pUn = cmdForm.Parameters.Add("@un", DbType.Int32);

                                    foreach (DataRow row in hojaFormuladas.Rows)
                                    {
                                        pAtc.Value = row["ATC"] ?? DBNull.Value;
                                        pLab.Value = row["Laboratorio"] ?? DBNull.Value;
                                        pProd.Value = row["Producto"] ?? DBNull.Value;
                                        pPres.Value = row["Presentacion"] ?? DBNull.Value;
                                        pUn.Value = row["Unidades"] ?? 0;
                                        cmdForm.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 3. Insertar filas de la Hoja Ventas
                            if (hojaVentas != null)
                            {
                                string queryVnt = "INSERT INTO DatosVentas (AuditoriaId, ATC, Laboratorio, Producto, Presentacion, Unidades, Valor) VALUES (@aId, @atc, @lab, @prod, @pres, @un, @val);";
                                using (var cmdVnt = new SQLiteCommand(queryVnt, conn, trans))
                                {
                                    cmdVnt.Parameters.Add("@aId", DbType.Int64).Value = auditoriaId;
                                    var pAtc = cmdVnt.Parameters.Add("@atc", DbType.String);
                                    var pLab = cmdVnt.Parameters.Add("@lab", DbType.String);
                                    var pProd = cmdVnt.Parameters.Add("@prod", DbType.String);
                                    var pPres = cmdVnt.Parameters.Add("@pres", DbType.String);
                                    var pUn = cmdVnt.Parameters.Add("@un", DbType.Int32);
                                    var pVal = cmdVnt.Parameters.Add("@val", DbType.Double);

                                    foreach (DataRow row in hojaVentas.Rows)
                                    {
                                        pAtc.Value = row["ATC"] ?? DBNull.Value;
                                        pLab.Value = row["Laboratorio"] ?? DBNull.Value;
                                        pProd.Value = row["Producto"] ?? DBNull.Value;
                                        pPres.Value = row["Presentacion"] ?? DBNull.Value;
                                        pUn.Value = row["Unidades"] ?? 0;
                                        pVal.Value = row["Valor"] ?? 0.0;
                                        cmdVnt.ExecuteNonQuery();
                                    }
                                }
                            }

                            trans.Commit(); // Confirmamos todos los inserts juntos
                        }
                        catch
                        {
                            trans.Rollback(); // Si algo falla, limpiamos para no dejar datos corruptos
                            throw;
                        }
                    }
                }
            });
        }

        // MÉTODO NUEVO: Recupera y reconstruye los DataTables originales a partir de un ID del historial
        public async Task<(DataTable Formuladas, DataTable Ventas)> RecuperarAuditoriaAsync(int auditoriaId)
        {
            return await Task.Run(() =>
            {
                DataTable dtFormuladas = new DataTable();
                // Definir esquema idéntico al que lee tu ExcelService original
                dtFormuladas.Columns.Add("ATC", typeof(string));
                dtFormuladas.Columns.Add("Laboratorio", typeof(string));
                dtFormuladas.Columns.Add("Producto", typeof(string));
                dtFormuladas.Columns.Add("Presentacion", typeof(string));
                dtFormuladas.Columns.Add("Unidades", typeof(int));

                DataTable dtVentas = dtFormuladas.Clone();
                dtVentas.Columns.Add("Valor", typeof(double));

                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();

                    // Cargar Formuladas
                    string queryForm = "SELECT ATC, Laboratorio, Producto, Presentacion, Unidades FROM DatosFormuladas WHERE AuditoriaId = @aId";
                    using (var cmd = new SQLiteCommand(queryForm, conn))
                    {
                        cmd.Parameters.AddWithValue("@aId", auditoriaId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dtFormuladas.Rows.Add(reader["ATC"], reader["Laboratorio"], reader["Producto"], reader["Presentacion"], reader["Unidades"]);
                            }
                        }
                    }

                    // Cargar Ventas
                    string queryVentas = "SELECT ATC, Laboratorio, Producto, Presentacion, Unidades, Valor FROM DatosVentas WHERE AuditoriaId = @aId";
                    using (var cmd = new SQLiteCommand(queryVentas, conn))
                    {
                        cmd.Parameters.AddWithValue("@aId", auditoriaId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dtVentas.Rows.Add(reader["ATC"], reader["Laboratorio"], reader["Producto"], reader["Presentacion"], reader["Unidades"], reader["Valor"]);
                            }
                        }
                    }
                }

                return (dtFormuladas, dtVentas);
            });
        }

        public async Task<List<AuditoriaHistorial>> ObtenerHistorialAsync()
        {
            return await Task.Run(() =>
            {
                var lista = new List<AuditoriaHistorial>();
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    string selectQuery = "SELECT Id, FechaRegistro, NombreArchivo, TotalRegistros, CoincidenciaCanales, Observaciones FROM HistorialAuditorias ORDER BY FechaRegistro DESC;";
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