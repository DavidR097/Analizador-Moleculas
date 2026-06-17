using System.ComponentModel;

namespace AnalizadorMoleculas.Models
{
    public class EspejoComparativo : INotifyPropertyChanged
    {
        private string _atc;
        private string _laboratorio;
        private string _producto;
        private string _packPx;
        private string _packUn;
        private double _totalRecetado;
        private double _totalVendido;

        public string Atc
        {
            get => _atc;
            set { _atc = value; OnPropertyChanged(nameof(Atc)); }
        }

        public string Laboratorio
        {
            get => _laboratorio;
            set { _laboratorio = value; OnPropertyChanged(nameof(Laboratorio)); }
        }

        public string Producto
        {
            get => _producto;
            set { _producto = value; OnPropertyChanged(nameof(Producto)); }
        }

        public string PackPx
        {
            get => _packPx ?? "";
            set { _packPx = value; OnPropertyChanged(nameof(PackPx)); }
        }

        public string PackUn
        {
            get => _packUn ?? "";
            set { _packUn = value; OnPropertyChanged(nameof(PackUn)); }
        }

        public double TotalRecetado
        {
            get => _totalRecetado;
            set { _totalRecetado = value; OnPropertyChanged(nameof(TotalRecetado)); }
        }

        public double TotalVendido
        {
            get => _totalVendido;
            set { _totalVendido = value; OnPropertyChanged(nameof(TotalVendido)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}