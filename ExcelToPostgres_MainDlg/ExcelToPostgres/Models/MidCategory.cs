using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExcelToPostgres.Models
{
    public class MidCategory : INotifyPropertyChanged
    {
        // NOT NULL 필드
        private string _midCatCode = "";
        private string _midCatName = "";
        private string _subCatCode = "";
        
        // NULL 허용 필드
        private string _midCatNameKr = null;
        private int _sortOrder;
        private bool _isActive = true;
        private string _description = null;

        // NOT NULL 필드
        public string MidCatCode
        {
            get { return _midCatCode; }
            set { _midCatCode = value ?? ""; OnPropertyChanged(); }
        }

        public string MidCatName
        {
            get { return _midCatName; }
            set { _midCatName = value ?? ""; OnPropertyChanged(); }
        }

        public string SubCatCode
        {
            get { return _subCatCode; }
            set { _subCatCode = value ?? ""; OnPropertyChanged(); }
        }

        // NULL 허용 필드
        public string MidCatNameKr
        {
            get { return _midCatNameKr; }
            set { _midCatNameKr = value; OnPropertyChanged(); }
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
