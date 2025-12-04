using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExcelToPostgres.Models
{
    public class MainCategory : INotifyPropertyChanged
    {
        // NOT NULL 필드
        private string _mainCatCode = "";
        private string _mainCatName = "";
        
        // NULL 허용 필드
        private string _mainCatNameKr = null;
        private bool _isStandard;
        private string _colorCode = null;
        private int _sortOrder;
        private bool _isActive = true;
        private string _description = null;

        // NOT NULL 필드
        public string MainCatCode
        {
            get { return _mainCatCode; }
            set { _mainCatCode = value ?? ""; OnPropertyChanged(); }
        }

        public string MainCatName
        {
            get { return _mainCatName; }
            set { _mainCatName = value ?? ""; OnPropertyChanged(); }
        }

        // NULL 허용 필드
        public string MainCatNameKr
        {
            get { return _mainCatNameKr; }
            set { _mainCatNameKr = value; OnPropertyChanged(); }
        }

        public bool IsStandard
        {
            get { return _isStandard; }
            set { _isStandard = value; OnPropertyChanged(); }
        }

        public string ColorCode
        {
            get { return _colorCode; }
            set { _colorCode = value; OnPropertyChanged(); }
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
