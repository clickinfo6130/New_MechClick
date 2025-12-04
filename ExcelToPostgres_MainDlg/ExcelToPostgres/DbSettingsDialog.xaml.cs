using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ExcelToPostgres
{
    /// <summary>
    /// DbSettingsDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DbSettingsDialog : Window
    {
        public string Host { get; private set; }
        public int Port { get; private set; }
        public string Database { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        public DbSettingsDialog(string host, int port, string database, string username, string password)
        {
            InitializeComponent();

            Host = host;
            Port = port;
            Database = database;
            Username = username;
            Password = password;

            TxtHost.Text = host;
            TxtPort.Text = port.ToString();
            TxtDatabase.Text = database;
            TxtUsername.Text = username;
            TxtPassword.Password = password;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Host = TxtHost.Text.Trim();
            
            int port;
            if (!int.TryParse(TxtPort.Text, out port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("올바른 포트 번호를 입력하세요. (1-65535)", "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPort.Focus();
                return;
            }
            Port = port;

            Database = TxtDatabase.Text.Trim();
            if (string.IsNullOrEmpty(Database))
            {
                MessageBox.Show("데이터베이스 이름을 입력하세요.", "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDatabase.Focus();
                return;
            }

            Username = TxtUsername.Text.Trim();
            Password = TxtPassword.Password;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
