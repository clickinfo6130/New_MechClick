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
    /// ColorPickerDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ColorPickerDialog : Window
    {
        public string SelectedColorCode { get; private set; }

        public ColorPickerDialog()
        {
            InitializeComponent();
            SelectedColorCode = "#808080";
            TxtColorCode.Text = SelectedColorCode;
            UpdateColorPreview();
        }

        public ColorPickerDialog(string initialColor) : this()
        {
            if (!string.IsNullOrEmpty(initialColor))
            {
                SelectedColorCode = initialColor;
                TxtColorCode.Text = initialColor;
                UpdateColorPreview();
            }
        }

        private void TxtColorCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateColorPreview();
        }

        private void UpdateColorPreview()
        {
            try
            {
                var text = TxtColorCode.Text.Trim();
                if (!text.StartsWith("#"))
                    text = "#" + text;

                if (text.Length == 7 || text.Length == 9)
                {
                    var color = (Color)ColorConverter.ConvertFromString(text);
                    ColorPreview.Background = new SolidColorBrush(color);
                }
            }
            catch
            {
                ColorPreview.Background = new SolidColorBrush(Colors.Gray);
            }
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            var text = TxtColorCode.Text.Trim();
            if (!text.StartsWith("#"))
                text = "#" + text;

            SelectedColorCode = text;
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
