using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DimensionManager.Models
{
    public class DimensionKeyOption : INotifyPropertyChanged
    {
        private int _id;
        private string _partCode = "";
        private string _keyFieldName = "";
        private int _keyLevel;
        private string _keyValue = "";
        private string _parentKey = null;
        private int _sortOrder;
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

        public string KeyFieldName
        {
            get { return _keyFieldName; }
            set { _keyFieldName = value ?? ""; OnPropertyChanged(); }
        }

        public int KeyLevel
        {
            get { return _keyLevel; }
            set { _keyLevel = value; OnPropertyChanged(); }
        }

        public string KeyValue
        {
            get { return _keyValue; }
            set { _keyValue = value ?? ""; OnPropertyChanged(); }
        }

        public string ParentKey
        {
            get { return _parentKey; }
            set { _parentKey = value; OnPropertyChanged(); }
        }

        public int SortOrder
        {
            get { return _sortOrder; }
            set { _sortOrder = value; OnPropertyChanged(); }
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
