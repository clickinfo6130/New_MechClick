using System;
using System.Windows;
using DimensionManager.Services;

namespace DimensionManager
{
    public partial class DbSettingsDialog : Window
    {
        public string Host { get; private set; }
        public int Port { get; private set; }
        public string Database { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        public DbSettingsDialog()
        {
            InitializeComponent();
        }

        public DbSettingsDialog(string host, int port, string database, string username, string password)
            : this()
        {
            TxtHost.Text = host;
            TxtPort.Text = port.ToString();
            TxtDatabase.Text = database;
            TxtUsername.Text = username;
            TxtPassword.Password = password;
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = new PostgresService();
                int port;
                if (!int.TryParse(TxtPort.Text, out port)) port = 5432;
                
                service.SetConnection(TxtHost.Text, port, TxtDatabase.Text, TxtUsername.Text, TxtPassword.Password);
                
                bool success = await service.TestConnectionAsync();
                if (success)
                {
                    string version = await service.GetServerVersionAsync();
                    MessageBox.Show("연결 성공!\nPostgreSQL " + version, "연결 테스트", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("연결 실패", "연결 테스트", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("연결 실패:\n" + ex.Message, "연결 테스트", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Host = TxtHost.Text;
            int port;
            Port = int.TryParse(TxtPort.Text, out port) ? port : 5432;
            Database = TxtDatabase.Text;
            Username = TxtUsername.Text;
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
