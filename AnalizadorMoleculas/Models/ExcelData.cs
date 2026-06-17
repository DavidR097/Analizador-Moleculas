using System;
using System.Collections.Generic;
using System.Text;

namespace AnalizadorMoleculas.Models
{
    public class FilaOriginal
    {
        public string Molecula { get; set; }
        public string Presentacion { get; set; }
        public double Formuladas { get; set; }
        public double Vendidas { get; set; }
    }

    public class AnalisisItem
    {
        public string Molecula { get; set; }
        public double TotalFormuladas { get; set; }
        public string Presentacion { get; set; }
        public double TotalVendidas { get; set; }
    }
}
