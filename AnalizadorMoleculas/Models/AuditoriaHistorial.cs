using System;

namespace AnalizadorMoleculas.Models
{
    public class AuditoriaHistorial
    {
        public int Id { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string NombreArchivo { get; set; }
        public int TotalRegistros { get; set; }
        public string Observaciones { get; set; }
    }
}