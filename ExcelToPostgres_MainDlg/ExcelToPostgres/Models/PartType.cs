using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExcelToPostgres.Models
{
    public class PartType : INotifyPropertyChanged
    {
        // NOT NULL 필드
        private string _partTypeCode = "";
        private string _partTypeName = "";
        
        // NULL 허용 필드
        private string _partTypeNameKr = null;
        private string _subCatCode = null;
        private string _midCatCode = null;
        private string _vendorCode = null;
        private bool _hasSeries;
        private int _sortOrder;
        private bool _isActive = true;
        private string _description = null;

        // NOT NULL 필드
        public string PartTypeCode
        {
            get { return _partTypeCode; }
            set { _partTypeCode = value ?? ""; OnPropertyChanged(); }
        }

        public string PartTypeName
        {
            get { return _partTypeName; }
            set { _partTypeName = value ?? ""; OnPropertyChanged(); }
        }

        // NULL 허용 필드
        public string PartTypeNameKr
        {
            get { return _partTypeNameKr; }
            set { _partTypeNameKr = value; OnPropertyChanged(); }
        }

        public string SubCatCode
        {
            get { return _subCatCode; }
            set { _subCatCode = value; OnPropertyChanged(); }
        }

        public string MidCatCode
        {
            get { return _midCatCode; }
            set { _midCatCode = value; OnPropertyChanged(); }
        }

        public string VendorCode
        {
            get { return _vendorCode; }
            set { _vendorCode = value; OnPropertyChanged(); }
        }

        public bool HasSeries
        {
            get { return _hasSeries; }
            set { _hasSeries = value; OnPropertyChanged(); }
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
