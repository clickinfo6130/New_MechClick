using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExcelToPostgres.Models
{
    public class PartSeries : INotifyPropertyChanged
    {
        // NOT NULL 필드
        private string _seriesCode = "";
        private string _seriesName = "";
        private string _partTypeCode = "";
        
        // NULL 허용 필드
        private string _seriesNameKr = null;
        private string _vendorCode = null;
        private string _modelPrefix = null;
        private int _sortOrder;
        private bool _isActive = true;
        private string _description = null;

        // NOT NULL 필드
        public string SeriesCode
        {
            get { return _seriesCode; }
            set { _seriesCode = value ?? ""; OnPropertyChanged(); }
        }

        public string SeriesName
        {
            get { return _seriesName; }
            set { _seriesName = value ?? ""; OnPropertyChanged(); }
        }

        public string PartTypeCode
        {
            get { return _partTypeCode; }
            set { _partTypeCode = value ?? ""; OnPropertyChanged(); }
        }

        // NULL 허용 필드
        public string SeriesNameKr
        {
            get { return _seriesNameKr; }
            set { _seriesNameKr = value; OnPropertyChanged(); }
        }

        public string VendorCode
        {
            get { return _vendorCode; }
            set { _vendorCode = value; OnPropertyChanged(); }
        }

        public string ModelPrefix
        {
            get { return _modelPrefix; }
            set { _modelPrefix = value; OnPropertyChanged(); }
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

        public string Description
        {
            get { return _description; }
            set { _description = value; OnPropertyChanged(); }
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
