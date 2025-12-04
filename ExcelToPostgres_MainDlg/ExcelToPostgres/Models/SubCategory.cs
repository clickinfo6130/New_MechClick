using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExcelToPostgres.Models
{
    public class SubCategory : INotifyPropertyChanged
    {
        // NOT NULL 필드 - 빈 문자열로 초기화
        private string _subCatCode = "";
        private string _subCatName = "";
        private string _mainCatCode = "";
        
        // NULL 허용 필드 - null로 초기화
        private string _subCatNameKr = null;
        private bool _isVendor;
        private string _vendorCode = null;
        private string _country = null;
        private int _sortOrder;
        private bool _isActive = true;
        private string _description = null;

        // NOT NULL 필드
        public string SubCatCode
        {
            get { return _subCatCode; }
            set { _subCatCode = value ?? ""; OnPropertyChanged(); }
        }

        public string SubCatName
        {
            get { return _subCatName; }
            set { _subCatName = value ?? ""; OnPropertyChanged(); }
        }

        public string MainCatCode
        {
            get { return _mainCatCode; }
            set { _mainCatCode = value ?? ""; OnPropertyChanged(); }
        }

        // NULL 허용 필드 - null 그대로 유지
        public string SubCatNameKr
        {
            get { return _subCatNameKr; }
            set { _subCatNameKr = value; OnPropertyChanged(); }
        }

        public bool IsVendor
        {
            get { return _isVendor; }
            set { _isVendor = value; OnPropertyChanged(); }
        }

        public string VendorCode
        {
            get { return _vendorCode; }
            set { _vendorCode = value; OnPropertyChanged(); }
        }

        public string Country
        {
            get { return _country; }
            set { _country = value; OnPropertyChanged(); }
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
