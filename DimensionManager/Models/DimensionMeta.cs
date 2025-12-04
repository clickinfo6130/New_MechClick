using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DimensionManager.Models
{
    public class DimensionMeta : INotifyPropertyChanged
    {
        private int _id;
        private string _partCode = "";
        private string _fieldName = "";
        private string _displayName = "";
        private string _displayNameEn = null;
        private string _dataType = "DECIMAL";
        private int _decimalPlaces = 2;
        private string _unit = null;
        private int _displayOrder;
        private bool _isKeyField = false;
        private bool _isDisplayField = true;
        private int _columnWidth = 80;
        private string _cadParamName = null;
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

        public string FieldName
        {
            get { return _fieldName; }
            set { _fieldName = value ?? ""; OnPropertyChanged(); }
        }

        public string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value ?? ""; OnPropertyChanged(); }
        }

        public string DisplayNameEn
        {
            get { return _displayNameEn; }
            set { _displayNameEn = value; OnPropertyChanged(); }
        }

        public string DataType
        {
            get { return _dataType; }
            set { _dataType = value ?? "DECIMAL"; OnPropertyChanged(); }
        }

        public int DecimalPlaces
        {
            get { return _decimalPlaces; }
            set { _decimalPlaces = value; OnPropertyChanged(); }
        }

        public string Unit
        {
            get { return _unit; }
            set { _unit = value; OnPropertyChanged(); }
        }

        public int DisplayOrder
        {
            get { return _displayOrder; }
            set { _displayOrder = value; OnPropertyChanged(); }
        }

        public bool IsKeyField
        {
            get { return _isKeyField; }
            set { _isKeyField = value; OnPropertyChanged(); }
        }

        public bool IsDisplayField
        {
            get { return _isDisplayField; }
            set { _isDisplayField = value; OnPropertyChanged(); }
        }

        public int ColumnWidth
        {
            get { return _columnWidth; }
            set { _columnWidth = value; OnPropertyChanged(); }
        }

        public string CadParamName
        {
            get { return _cadParamName; }
            set { _cadParamName = value; OnPropertyChanged(); }
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
