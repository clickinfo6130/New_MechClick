using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DimensionManager.Models
{
    public class PartDimension : INotifyPropertyChanged
    {
        private int _id;
        private string _partCode = "";
        private string _keyComposite = "";      // 'KS B 1002|M10|기계용'
        private string _keyValuesJson = "";     // {"Standard":"KS B 1002", "List":"M10", "Usage":"기계용"}
        private string _dimensionDataJson = ""; // {"M":10, "P1":1.5, "H":6.4, ...}
        private bool _isActive = true;

        public int Id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged(); }
        }

        public string PartCode
        {
            get { return _partCode; }
            set { _partCode = value ?? ""; OnPropertyChanged(); }
        }

        public string KeyComposite
        {
            get { return _keyComposite; }
            set { _keyComposite = value ?? ""; OnPropertyChanged(); }
        }

        public string KeyValuesJson
        {
            get { return _keyValuesJson; }
            set { _keyValuesJson = value ?? ""; OnPropertyChanged(); }
        }

        public string DimensionDataJson
        {
            get { return _dimensionDataJson; }
            set { _dimensionDataJson = value ?? ""; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
