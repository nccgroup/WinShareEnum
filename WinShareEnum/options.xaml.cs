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

namespace WinShareEnum
{
    /// <summary>
    /// Interaction logic for options.xaml
    /// </summary>
    public partial class options : Window
    {
        public options()
        {
            InitializeComponent();

            switch (MainWindow.logLevel)
            {
                case MainWindow.LOG_LEVEL.INTERESTINGONLY:
                    rb_useful.IsChecked = true;
                    break;
                case MainWindow.LOG_LEVEL.ERROR:
                    rb_error.IsChecked = true;
                    break;
                case MainWindow.LOG_LEVEL.INFO:
                    rb_info.IsChecked = true;
                    break;
          case MainWindow.LOG_LEVEL.DEBUG:
                    rb_debug.IsChecked = true;
                    break;
            }

            sl_threads.Value = MainWindow._parallelOption.MaxDegreeOfParallelism;
            lbl_Threads.Content = sl_threads.Value;
            
            foreach(string interesting in MainWindow.interestingFileList)
            {
                lb_interesting.Items.Add(interesting);
            }
            
            foreach(string fileContent in MainWindow.fileContentsFilters)
            {
                lb_fileContents.Items.Add(fileContent);
            }


            cb_recursiveSearch.IsChecked = MainWindow.recursiveSearch;

            cb_includeBinaryFiles.IsChecked = MainWindow.includeBinaryFiles;

            tb_max_fileSize.Text = MainWindow.MAX_FILESIZE.ToString();

        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sl_threads != null && lbl_Threads.Content != null)
            {
               lbl_Threads.Content = Math.Round(sl_threads.Value).ToString();
               MainWindow._parallelOption.MaxDegreeOfParallelism = int.Parse(Math.Round(sl_threads.Value).ToString());
            }
        }

        #region logging
        private void rb_debug_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.logLevel = MainWindow.LOG_LEVEL.DEBUG;
        }

        private void rb_info_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.logLevel = MainWindow.LOG_LEVEL.INFO;
        }

        private void rb_error_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.logLevel = MainWindow.LOG_LEVEL.ERROR;
        }

        private void rb_useful_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.logLevel = MainWindow.LOG_LEVEL.INTERESTINGONLY;
        }

        #endregion

        private void btn_interesting_delete_Click(object sender, RoutedEventArgs e)
        {
            if (lb_interesting.SelectedItem != null)
            {
                persistance.deleteInterestingRule(lb_interesting.SelectedValue.ToString());
                lb_interesting.Items.Remove(lb_interesting.SelectedItem);
            }

        }

        private void btn_interesting_add_Click(object sender, RoutedEventArgs e)
        {
            if (tb_interesting_newFilter.Text != "")
            {
                persistance.saveInterestingRule(tb_interesting_newFilter.Text);
                lb_interesting.Items.Add(tb_interesting_newFilter.Text);
                tb_interesting_newFilter.Text = "";
            }
        }

        private void btn_fileFilter_delete_Click(object sender, RoutedEventArgs e)
        {

            if (lb_fileContents.SelectedItem != null)
            {
                persistance.deleteFileContentRule(lb_fileContents.SelectedValue.ToString());
                lb_fileContents.Items.Remove(lb_fileContents.SelectedItem);
            }
        }

        private void btn_fileFilter_add_Click(object sender, RoutedEventArgs e)
        {

            if (tb_fileFilter_newFilter.Text != "")
            {
                persistance.saveFileContentRule(tb_fileFilter_newFilter.Text);
                lb_fileContents.Items.Add(tb_fileFilter_newFilter.Text);
                tb_fileFilter_newFilter.Text = "";
            }
        }

        private void tb_max_fileSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            int fileSize;
            if (!int.TryParse(tb_max_fileSize.Text, out fileSize))
            {
                MessageBox.Show("Filesize can only be a number", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MainWindow.MAX_FILESIZE = fileSize;
            }
        }

        private void cb_recursiveSearch_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.recursiveSearch = true;
        }

        private void cb_recursiveSearch_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow.recursiveSearch = false;
        }

        private void cb_includeBinaryFiles_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.includeBinaryFiles = true;
        }

        private void cb_includeBinaryFiles_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow.includeBinaryFiles = false;
        }


    }
}
