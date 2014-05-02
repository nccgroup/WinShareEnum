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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Management;
using System.Data;
using System.ComponentModel;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Reflection;
using System.Diagnostics;

namespace WinShareEnum
{
    public partial class MainWindow : Window
    {

        #region variables
        public static string USERNAME = "";
        public static string PASSSWORD = "";
        public static List<string> interestingFileList = new List<string>() { "web.conf", "credentials", "credentials.*", "###\\b(?:\\d{1,3}\\.){3}\\d{1,3}\\b", "creds", "creds.*", "shadow", ".bashrc", "secret", "secret.*", "*.pem", "password.*", ".htaccess", "key.*", "privatekey.*", "private_key.*", "global.asax", "pwned.*", "*.key", "*.pkcs12", "*.pfx", "*.p12", "*.crt" };
        public static List<string> fileContentsFilters = new List<string>() { "BEGIN PRIVATE KEY", "BEGIN RSA PRIVATE KEY", "password=", "password =", "pass=", "pass = ", "password:", "password :", "username =", "user =", "username=", "user="};
        
        public static CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        public static ParallelOptions _parallelOption = new ParallelOptions { MaxDegreeOfParallelism = 30, CancellationToken = _cancellationToken.Token };
     
        public static LOG_LEVEL logLevel = LOG_LEVEL.ERROR;

        public static bool recursiveSearch = true;
        public static bool optionsOpen = false;
        public static bool autoScroll = true;
        public static bool includeBinaryFiles = false;
        public static bool useImportedIPs = false;

        public const int ICON_HEIGHT = 15;
        public const int ICON_WIDTH = 18;
        public const int TIMEOUT = 15000;
        public static int NUMBER_FILES_PROCESSED = 0;
        public static int MAX_FILESIZE = 250;
        public static bool AUTHLOCALLY = false;


        public static System.Windows.Media.SolidColorBrush brush_EveryoneRead = Brushes.Red;
        public static System.Windows.Media.SolidColorBrush brush_currentUserRead = Brushes.Blue;
        #endregion

        public static ConcurrentQueue<string> loglist;
        public static ConcurrentDictionary<string, List<shareStruct>> all_readable_shares = new ConcurrentDictionary<string, List<shareStruct>>();
        public static ConcurrentDictionary<string, Dictionary<string, List<string>>> all_readable_files = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();
        public ConcurrentBag<string> all_interesting_files = new ConcurrentBag<string>();
        public static List<IPAddress> ImportedIPs = new List<IPAddress>();

        public enum GenericRights : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_EXECUTE = 0x20000000,
            GENERIC_ALL = 0x10000000
        }
        public enum MappedGenericRights
        {
            FILE_GENERIC_EXECUTE = FileSystemRights.ExecuteFile | FileSystemRights.ReadPermissions | FileSystemRights.ReadAttributes | FileSystemRights.Synchronize,
            FILE_GENERIC_READ = FileSystemRights.ReadAttributes | FileSystemRights.ReadData | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadPermissions | FileSystemRights.Synchronize,
            FILE_GENERIC_WRITE = FileSystemRights.AppendData | FileSystemRights.WriteAttributes | FileSystemRights.WriteData | FileSystemRights.WriteExtendedAttributes | FileSystemRights.ReadPermissions | FileSystemRights.Synchronize,
            FILE_GENERIC_ALL = FileSystemRights.FullControl
        }
        public enum LOG_LEVEL
        {
            DEBUG,
            INFO,
            ERROR,
            INTERESTINGONLY
        }

        public struct shareStruct
        {
            public string shareName;
            public string domain;
            public string ipAddressHostname;
            public FileSystemRights everyoneRights;
            public FileSystemRights currentUserRights;
            public bool currentUserCanRead;
            public bool everyoneCanRead;
            public AuthorizationRuleCollection permissionsList;

        }
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                loglist = new ConcurrentQueue<string>();
                //get saved stuff, if it is first run, add all of the hardcoded stuff to settings, save them, then null out hardcoded stuff and work purely with saved stuff in future
                persistance p = new persistance();

                fileContentsFilters = new List<string>();
                foreach (string s in persistance.getFileContentRules())
                {
                    fileContentsFilters.Add(s);
                }

                interestingFileList = new List<string>();
                foreach (string s in persistance.getInterestingFiles())
                {
                    interestingFileList.Add(s);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region misc GUI crap

        private void tbUsername_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tbUsername.Text != "")
            {
                USERNAME = tbUsername.Text;
            }
        }


        private void tbIPRange_TextChanged(object sender, TextChangedEventArgs e)
        {
            //assume since they changed the IP list, they no longer want to use their imported list..
            if (tbIPRange.Text.ToLower() != "using imported list")
            {
                useImportedIPs = false;
            }
        }

        private void tbPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (tbPassword.Password != "")
            {
                PASSSWORD = tbPassword.Password;
            }
        }

        private void tbUsername_GotFocus(object sender, RoutedEventArgs e)
        {
            tbUsername.Text = "";
        }

        private void tbPassword_GotFocus(object sender, RoutedEventArgs e)
        {
            tbPassword.Password = "";
        }

        private void tbPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            if (tbPassword.Password == "")
            {
                tbPassword.Password = PASSSWORD;
            }
        }

        private void tbUsername_LostFocus(object sender, RoutedEventArgs e)
        {
            if (tbUsername.Text == "")
            {
                tbUsername.Text = USERNAME;
            }
        }

        private void checkbox_Null_Checked(object sender, RoutedEventArgs e)
        {
            tbUsername.IsEnabled = false;
            tbPassword.IsEnabled = false;
            USERNAME = "";
            PASSSWORD = "";
            tbUsername.Text = "DOMAIN\\USER";
            tbPassword.Password = "password";
        }

        private void checkbox_Null_Unchecked(object sender, RoutedEventArgs e)
        {
            tbUsername.IsEnabled = true;
            tbPassword.IsEnabled = true;
        }

        private void resetGUI()
        {
            all_readable_shares = new ConcurrentDictionary<string, List<shareStruct>>();
            all_readable_files = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();
            all_interesting_files = new ConcurrentBag<string>();
            btnFindInterestingFiles.IsEnabled = false;
            btnGrepFiles.IsEnabled = false;
            treeviewMain.Items.Clear();
            pgbMain.Value = 0;
            pgbMain.Maximum = 0;
            SetGlowVisibility(pgbMain, Visibility.Hidden);
            NUMBER_FILES_PROCESSED = 0;
        }

        private void addToResultsList(string pathName, string filename, string comment = "")
        {

            dgItem dg = new dgItem();
            dg.Comment = comment;
            dg.Name = filename;
            dg.Path = pathName;

            Dispatcher.Invoke((Action)delegate { lv_resultsList.Items.Add(dg); });



        }

        public void addLog(string item, bool isDeadMessage = false)
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                loglist.Enqueue(DateTime.Now + " - " + item);
            }
            else if (isDeadMessage == true)
            {
                loglist.Enqueue(DateTime.Now + " - " + item);
                resetTokens();
            }
            while (!loglist.IsEmpty)
            {
                string res = "";
                loglist.TryDequeue(out res);
                lbLog.Items.Add(res);

                if (autoScroll == true)
                {
                    lbLog.SelectedIndex = lbLog.Items.Count - 1;
                    lbLog.ScrollIntoView(lbLog.SelectedItem);
                }
            }

        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            autoScroll = true;
            lbLog.SelectedIndex = lbLog.Items.Count - 1;
            lbLog.ScrollIntoView(lbLog.SelectedItem);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            autoScroll = false;
        }

        private void addSharesToTreeview(List<shareStruct> item)
        {
            TreeViewItem ti = new TreeViewItem();
            ti.Header = item[0].ipAddressHostname;
            bool canEveryoneRead = false;
            bool canUserRead = false;

            //share names duplicated, prevent adding them to the treeview (only add most accessible)
            List<string> duplicatesList = new List<string>();

            //sub-trees for each share of the IP/Host
            foreach (var _shareStruct in item)
            {

                if (!duplicatesList.Contains(_shareStruct.shareName))
                {
                    duplicatesList.Add(_shareStruct.shareName);

                    if (logLevel < LOG_LEVEL.ERROR)
                    {
                        addLog("Share found: " + _shareStruct.ipAddressHostname + "\\" + _shareStruct.shareName);
                    }
                    TreeViewItem ti2 = new TreeViewItem();
                    ti2.Header = _shareStruct.shareName;
                    ti.Items.Add(ti2);

                }
            }
            foreach (shareStruct _shareStruct in item)
            {
                //sort colour
                foreach (TreeViewItem treeItem in ti.Items)
                {
                    // Full Access = 2032127, Modify = 1245631, Read Write = 118009, Read Only = 1179817
                    //everyone can read/write or full perms
                    if (_shareStruct.shareName == (string)treeItem.Header && _shareStruct.everyoneCanRead == true)
                    {
                        treeItem.Foreground = brush_EveryoneRead;
                        if (logLevel <= LOG_LEVEL.INTERESTINGONLY)
                        {
                            addLog("world-readable share found: " + _shareStruct.ipAddressHostname + "\\" + _shareStruct.shareName);
                        }

                        canEveryoneRead = true;
                    }
                    else if (_shareStruct.shareName == (string)treeItem.Header && _shareStruct.currentUserCanRead == true)
                    {
                        treeItem.Foreground = brush_currentUserRead;
                        if (logLevel <= LOG_LEVEL.INTERESTINGONLY)
                        {
                            addLog("User-readable share found: " + _shareStruct.ipAddressHostname + "\\" + _shareStruct.shareName);
                        }

                        canUserRead = true;
                    }




                }
            }

            if (canEveryoneRead == true)
            {
                StackPanel holder = new StackPanel();
                holder.Orientation = Orientation.Horizontal;
                holder.Children.Add(new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Resources/low.png")), Height = ICON_HEIGHT, Width = ICON_WIDTH });
                holder.Children.Add(new TextBlock() { Text = ti.Header.ToString(), VerticalAlignment = System.Windows.VerticalAlignment.Center });


                ti.Header = holder;
                ti.Foreground = brush_EveryoneRead;
            }
            else if (canUserRead == true)
            {
                ti.Foreground = brush_currentUserRead;
                StackPanel holder = new StackPanel();
                holder.Orientation = Orientation.Horizontal;
                holder.Children.Add(new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/Resources/info.png")), Height = ICON_HEIGHT, Width = ICON_WIDTH });
                holder.Children.Add(new TextBlock() { Text = ti.Header.ToString(), VerticalAlignment = System.Windows.VerticalAlignment.Center });

                ti.Header = holder;

            }
            treeviewMain.Items.Add(ti);
        }

        private void updateNumberofFilesProcessedLabel()
        {
            Interlocked.Increment(ref NUMBER_FILES_PROCESSED);
            Dispatcher.Invoke((Action)delegate { lbl_fileCount.Content = "Files Processed: " + NUMBER_FILES_PROCESSED; });
        }

        private void SetGlowVisibility(ProgressBar progressBar, Visibility visibility)
        {
            var glow = progressBar.Template.FindName("PART_GlowRect", progressBar) as FrameworkElement;
            if (glow != null) glow.Visibility = visibility;
        }

        private void treeviewMain_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {

                TreeViewItem item = treeviewMain.SelectedItem as TreeViewItem;

                TreeView tv = (TreeView)sender;
                TreeViewItem child = (TreeViewItem)tv.SelectedItem;

                if (child.Parent.GetType() == typeof(TreeViewItem))
                {
                    TreeViewItem parent = (TreeViewItem)child.Parent;
                    if (parent.Header.GetType() != typeof(StackPanel))
                    {
                        tb_SelectedSharePerms.Text = "N/A";
                        tb_SelectedSharePerms.IsEnabled = false;
                        return;
                    }

                    StackPanel sp = (StackPanel)parent.Header;
                    TextBlock tb = (TextBlock)sp.Children[1];


                    string text = tb.Text;
                    string text2 = item.Header.ToString();
                    StringBuilder sb = new StringBuilder();
                    bool FoundShare = false;

                    if (all_readable_shares.ContainsKey(text))
                    {
                        sb.Append(@"\\" + text + "\\" + text2 + ":");
                        foreach (shareStruct ss in all_readable_shares[text])
                        {
                            if (ss.shareName == text2)
                            {
                                FoundShare = true;
                                foreach (FileSystemAccessRule fas in ss.permissionsList)
                                {
                                    sb.Append("\r\n\t- " + fas.IdentityReference.ToString());
                                    sb.Append("\r\n\t\t--" + MapGenericRightsToFileSystemRights(fas.FileSystemRights));
                                }
                            }

                        }
                        if (FoundShare == false)
                        {
                            sb = new StringBuilder();
                            sb.Append(@"\\" + text + "\\" + text2 + ":");
                            sb.Append("\r\n\t- Unable to enumerate share permissions.");
                            tb_SelectedSharePerms.IsEnabled = false;
                        }
                        else
                        {
                            tb_SelectedSharePerms.IsEnabled = true;
                        };

                        tb_SelectedSharePerms.Text = sb.ToString();
                    }
                }
                //not a treeviewitem
                else
                {
                    tb_SelectedSharePerms.Text = "N/A";
                    tb_SelectedSharePerms.IsEnabled = false;
                }
            }
            catch (Exception)
            {
                //do nothing
            }

        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //update the "path" column so it is always stretched,  like lindsay lohan
            gvtest.Columns[3].Width = gb_EnumResults.ActualWidth - gvtest.Columns[0].Width - gvtest.Columns[1].Width - gvtest.Columns[2].Width;
        }

        #endregion

        #region buttons/menus

        private async void btnGO_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (checkbox_Null.IsChecked == false)
                {
                    string[] splat = tbUsername.Text.Split('\\');
                    if (splat.Length < 2)
                    {
                        throw new Exception("Enter a Domain");
                    }
                    else if (tbUsername.Text.ToLower() == "domain\\user")
                    {
                        throw new Exception("Enter credentials");
                    }

                    AUTHLOCALLY = splat[0] == "." ? true : false;
                }

                btn_Stop.IsEnabled = true;
                btn_Stop.Visibility = Visibility.Visible;
                btnGO.Visibility = System.Windows.Visibility.Hidden;
                resetGUI();
                List<IPAddress> ip_list = new List<IPAddress>();

                if (useImportedIPs == true)
                {
                    ip_list = ImportedIPs;
                }
                else
                {
                    try
                    {
                        IPRange ipr = new IPRange(tbIPRange.Text.Trim());
                        ip_list = ipr.GetAllIP().ToList<IPAddress>();

            

                    }
                    catch (Exception)
                    {
                        throw new Exception("Invalid IP Range Entered.");
                    }
                }

                pgbMain.Maximum = ip_list.Count;
                addLog("Starting share enumeration of " + ip_list.Count + " servers (" + _parallelOption.MaxDegreeOfParallelism + " threads)...");

                pgbMain.Visibility = Visibility.Visible;

                List<Task<List<shareStruct>>> tList = new List<Task<List<shareStruct>>>();

                try
                {
                    await Task.Run(() =>
                    {
                        Parallel.ForEach(ip_list, _parallelOption, item =>
                            tList.Add(_populateShareStructsTimeout(item.ToString())));
                    });
                }

                catch (OperationCanceledException)
                {
                        addLog("Threads dead, baby. Threads dead.", true);
                        btn_Stop.Visibility = System.Windows.Visibility.Hidden;
                        btnGO.Visibility = System.Windows.Visibility.Visible;
                        pgbMain.Visibility = Visibility.Hidden;
                        btn_Stop.IsEnabled = true;
                        return;
                }



                pgbMain.Visibility = Visibility.Hidden;
                addLog("Share enumeration Complete.");



                btnGO.Visibility = System.Windows.Visibility.Visible;


                int totalServers = all_readable_shares.Count;
                int totalShares = 0;
                int everyoneReadable = 0;
                int userReadable = 0;


                foreach (var lss in all_readable_shares.Keys)
                {
                    totalShares += all_readable_shares[lss].Count;
                    foreach (shareStruct ss in all_readable_shares[lss])
                    {
                        if (ss.everyoneCanRead == true)
                        {
                            everyoneReadable++;
                            userReadable++;
                        }
                        else if (ss.currentUserCanRead == true)
                        {
                            userReadable++;
                        }
                    }
                }

                if (totalServers > 0)
                {
                    btnFindInterestingFiles.IsEnabled = true;
                    btnGrepFiles.IsEnabled = true;
                }
                addLog(userReadable + " shares readable by current user on " + totalServers + " servers");
                addLog(everyoneReadable + " shares readable by everyone");

            }
                catch (OperationCanceledException)
            {

                addLog("Threads dead, baby. Threads dead.", true);
                btn_Stop.Visibility = System.Windows.Visibility.Hidden;
                btnGO.Visibility = System.Windows.Visibility.Visible;
                btn_Stop.IsEnabled = true;
                return;
            }

            catch (Exception ex)
            {

                if (ex.InnerException != null && ex.InnerException.Message != null && ex.InnerException.Message == "The operation was canceled.")
                {
                    addLog("Threads dead, baby. Threads dead.", true);
                    btn_Stop.Visibility = System.Windows.Visibility.Hidden;
                    btnGO.Visibility = System.Windows.Visibility.Visible;
                    btn_Stop.IsEnabled = true;
                    return;
                }

                else
                {

                    btnGO.Visibility = System.Windows.Visibility.Visible;
                    pgbMain.Visibility = Visibility.Hidden;
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    if (logLevel < LOG_LEVEL.INFO)
                    {
                        addLog("Bad exception " + ex.Message + ex.StackTrace);
                    }
                    else
                    {
                        addLog(ex.Message);
                    }
                }
            }
            btn_Stop.Visibility = Visibility.Hidden;
        }

        private void mi_Options_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.Windows.Count == 1)
            {
                options o = new options();
                o.Show();
            }
            else
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (!w.IsActive)
                    {
                        w.Activate();
                    }

                }
            }
        }


        private void btn_clearResults_Click(object sender, RoutedEventArgs e)
        {
            lv_resultsList.Items.Clear();
        }

        private async void btFindInterestingFiles_Click(object sender, RoutedEventArgs e)
        {
            btnFindInterestingFiles.IsEnabled = false;
            btn_StopInteresting.IsEnabled = true;
            btn_StopInteresting.Visibility = System.Windows.Visibility.Visible;
            btnFindInterestingFiles.Visibility = System.Windows.Visibility.Hidden;


            try
            {


                List<string> shareList = new List<string>();
                ConcurrentBag<bool> finalInteresting = new ConcurrentBag<bool>();
                foreach (string serverName in all_readable_shares.Keys)
                {
                    foreach (shareStruct ss in all_readable_shares[serverName])
                    {
                        shareList.Add(serverName + "\\" + ss.shareName);
                    }
                }

                pgbMain.Visibility = System.Windows.Visibility.Visible;

                pgbMain.Maximum = shareList.Count;
                pgbMain.Value = 0;
                addLog("Searching for interesting files on " + shareList.Count + " shares...");


                await Task.Run(() =>
                 {
                     Parallel.ForEach(shareList, _parallelOption, item =>
                         finalInteresting.Add(getInterstingFileList(item)));
                 });

                pgbMain.Visibility = System.Windows.Visibility.Hidden;
                finalInteresting = new ConcurrentBag<bool>();

                addLog("Searching for interesting files complete.");
                btnFindInterestingFiles.IsEnabled = true;
                btn_StopInteresting.IsEnabled = false;
                btn_StopInteresting.Visibility = System.Windows.Visibility.Hidden;
                btnFindInterestingFiles.Visibility = System.Windows.Visibility.Visible;
            }

            catch (OperationCanceledException)
            {
                addLog("Threads dead, baby. Threads dead.", true);
                //reset file dict
                all_readable_files = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();

                btn_StopInteresting.Visibility = System.Windows.Visibility.Hidden;
                btnFindInterestingFiles.Visibility = System.Windows.Visibility.Visible;
                btn_StopInteresting.IsEnabled = false;
                btnFindInterestingFiles.IsEnabled = true;
                pgbMain.Visibility = System.Windows.Visibility.Hidden;
                return;
            }

            catch (Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message != null && ex.InnerException.Message == "The operation was canceled.")
                {

                    addLog("Threads dead, baby. Threads dead.", true);
                    //reset file dict
                    all_readable_files = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();
                    btnFindInterestingFiles.IsEnabled = true;
                    btn_StopInteresting.Visibility = System.Windows.Visibility.Hidden;
                    btnFindInterestingFiles.Visibility = System.Windows.Visibility.Visible;
                    btn_StopInteresting.IsEnabled = true;
                    pgbMain.Visibility = System.Windows.Visibility.Hidden;
                    return;

                }
                else
                {
                    pgbMain.Visibility = Visibility.Hidden;
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    if (logLevel < LOG_LEVEL.INFO)
                    {
                        addLog("Bad exception " + ex.Message + ex.StackTrace);
                    }
                    else
                    {
                        addLog(ex.Message);
                    }
                }
            }
            btnFindInterestingFiles.IsEnabled = true;
        }

        private async void btnGrepFiles_Click(object sender, RoutedEventArgs e)
        {
            btn_StopGrep.IsEnabled = true;
            btn_StopGrep.Visibility = System.Windows.Visibility.Visible;
            btnGrepFiles.Visibility = Visibility.Hidden;


            try
            {
                List<string> shareList = new List<string>();
                ConcurrentBag<bool> finalInteresting = new ConcurrentBag<bool>();
                foreach (string serverName in all_readable_shares.Keys)
                {
                    foreach (shareStruct ss in all_readable_shares[serverName])
                    {
                        shareList.Add(serverName + "\\" + ss.shareName);
                    }
                }

                pgbMain.Visibility = System.Windows.Visibility.Visible;

                pgbMain.Maximum = shareList.Count;
                pgbMain.Value = 0;
                addLog("Searching File Contents on " + shareList.Count + " shares...");


                await Task.Run(() =>
                {
                    Parallel.ForEach(shareList, _parallelOption, item =>
                        finalInteresting.Add(getFileContentsList(item)));
                });

                pgbMain.Visibility = System.Windows.Visibility.Hidden;
                finalInteresting = new ConcurrentBag<bool>();

                addLog("Searching file contents complete.");

            }

            catch (OperationCanceledException)
            {

                addLog("Threads dead, baby. Threads dead.", true);
                //reset file dict
                all_readable_files = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();
                //todo: change
                btn_StopGrep.Visibility = System.Windows.Visibility.Hidden;
                btnGrepFiles.Visibility = Visibility.Visible;
                btn_StopGrep.IsEnabled = true;
                pgbMain.Visibility = Visibility.Hidden;
                return;
            }

            catch (Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message != null && ex.InnerException.Message == "The operation was canceled.")
                {
                    addLog("Threads dead, baby. Threads dead.", true);
                    //reset file dict
                    all_readable_files = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();
                    //todo: change
                    btn_StopGrep.Visibility = System.Windows.Visibility.Hidden;
                    btnGrepFiles.Visibility = Visibility.Visible;
                    btn_StopGrep.IsEnabled = true;
                    pgbMain.Visibility = Visibility.Hidden;
                    return;

                }

                pgbMain.Visibility = Visibility.Hidden;
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                if (logLevel < LOG_LEVEL.INFO)
                {
                    addLog("Bad exception " + ex.Message + ex.StackTrace);
                }
                else
                {
                    addLog(ex.Message);
                }
            }
            btnGrepFiles.IsEnabled = true;
            btn_StopGrep.IsEnabled = false;
            btn_StopGrep.Visibility = System.Windows.Visibility.Hidden;
            btnGrepFiles.Visibility = Visibility.Visible;
        }

        private void mi_copyAllSharesandPerms_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            foreach (string key in all_readable_shares.Keys)
            {
                foreach (shareStruct ss in all_readable_shares[key])
                {
                    sb.Append("\r\n\r\n\r\n" + @"\\" + key + "\\" + ss.shareName + ":" + "\r\n");

                    foreach (FileSystemAccessRule fas in ss.permissionsList)
                    {
                        sb.Append("\r\n\t- " + fas.IdentityReference.ToString());
                        sb.Append("\r\n\t\t--" + MapGenericRightsToFileSystemRights(fas.FileSystemRights));
                    }
                }
            }

            Clipboard.SetText(sb.ToString());
        }

        private void btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Really Stop?", "Stop", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                addLog("Stopping background threads...");
                _cancellationToken.Cancel();
                btn_Stop.IsEnabled = false;
                btn_StopGrep.IsEnabled = false;
                btn_StopInteresting.IsEnabled = false;
            }
        }

        private void download_file(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            dgItem aa = b.CommandParameter as dgItem;

          

            string[] basepath = aa.Path.Split('\\');

            var oNetworkCredential = getNetworkCredentials(basepath[2]);

            using (new RemoteAccessHelper.NetworkConnection(@"\\" + basepath[2] + "\\" + basepath[3], oNetworkCredential, false))
            {
                try
                {
                    File.Copy(aa.Path, Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + aa.Name, true);
                    addLog("Downloaded " + aa.Name + " to desktop.");
                }
                catch (Exception ex)
                {
                    if (logLevel <= LOG_LEVEL.ERROR)
                    {
                        addLog("Error downloading file: " + ex.Message);
                    }
                }
            }


        }

        private void mi_CopyLog_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            foreach (string s in lbLog.Items)
            {
                sb.Append(s + "\r\n");
            }
            Clipboard.SetText(sb.ToString());
        }

        private void mi_copyEveryoneShares_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            foreach (List<shareStruct> ssList in all_readable_shares.Values)
            {
                foreach (shareStruct ss in ssList)
                {
                    if (ss.everyoneCanRead == true)
                    {
                        sb.Append("\r\n\r\n" + @"\\" + ss.ipAddressHostname + "\\" + ss.shareName + ":");
                        foreach (FileSystemAccessRule fas in ss.permissionsList)
                        {
                            if (fas.IdentityReference.ToString().ToLower() == "everyone")
                            {
                                sb.Append("\r\n\t- " + fas.IdentityReference.ToString());
                                sb.Append("\r\n\t\t--" + MapGenericRightsToFileSystemRights(fas.FileSystemRights));
                            }

                        }
                    }
                }
            }
            Clipboard.SetText(sb.ToString());
        }

        private void mi_CopyResultsPane_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            foreach(dgItem item in lv_resultsList.Items)
            {
                sb.Append(item.Path + "\t\t" + item.Comment + "\r\n");
            }
            Clipboard.SetText(sb.ToString());
        }

        private void mi_version_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Windows Share Enumerator\r\nVersion: " + updates.getCurrentVersion().ToString() + "\r\nJonathan.Murray@nccgroup.com", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void mi_updateRules_Click(object sender, RoutedEventArgs e)
        {
            int count = 0;
            try
            {
                List<string> fileFilterUpdates = updates.getFileFilterUpdates();
                foreach (string update in fileFilterUpdates)
                {
                    if (!Settings.Default.FileContentRules.Contains(update) && update != "")
                    {
                        Settings.Default.FileContentRules.Add(update);
                        count++;
                        addLog("Added file filter rule " + update);
                    }
                }

                Settings.Default.Save();

                List<string> interestingUpdates = updates.getInterestingFileUpdates();
                foreach (string update in interestingUpdates)
                {
                    if (!Settings.Default.interestingFileNameRules.Contains(update) && update != "")
                    {
                        Settings.Default.interestingFileNameRules.Add(update);
                        count++;
                        addLog("Added interesting file rule " + update);
                    }
                }

                Settings.Default.Save();
                MessageBox.Show("Rules update complete. " + count + " new rules added.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating rules. " + ex.Message, "Update Rules Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                addLog("Error " + ex.Message + " -- " + ex.StackTrace);
            }



        }

        private void mi_checkAppUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (updates.getCurrentVersion() < updates.getLatestVersion())
                {
                    MessageBoxResult mbr = MessageBox.Show("New version available, want to download it? \r\n\r\nNote: this will download to desktop and overwrite..", "Update", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (mbr == MessageBoxResult.Yes)
                    {
                        try
                        {
                            addLog("Downloading most recent version to desktop..");
                            updates.downloadUpdate();
                            addLog(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\WinShareEnum.exe downloaded.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            addLog("Error " + ex.Message + " -- " + ex.StackTrace);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("No New Version Available", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show("Error getting current version. " + ex.Message, "Update Rules Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                addLog("Error " + ex.Message + " -- " + ex.StackTrace);
            }
        }

        private void mi_importIPs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            dlg.DefaultExt = ".*";
            dlg.Filter = "Any Files (*.*)|*.*|Text Files (*.txt)|*.txt";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                addLog("Adding IPs from " + dlg.FileName);
                resetGUI();
                useImportedIPs = true;
                tbIPRange.Text = "Using Imported";

                ImportedIPs = new List<IPAddress>();
                try
                {
                    string line;
                    int fileEntries = 0;
                    int totalEntries = 0;

                    using (StreamReader reader = new StreamReader(dlg.FileName))
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            try
                            {
                                fileEntries++;
                                IPRange ipr = new IPRange(line);
                                foreach (IPAddress ip in ipr.GetAllIP())
                                {
                                    ImportedIPs.Add(ip);
                                    totalEntries++;
                                }
                            }
                            catch (Exception)
                            {
                                addLog("Error - failed to parse " + line + " as a valid IP range, skipping it..");
                            }
                        }
                    }

                    addLog("Successfully added " + fileEntries + " entries, " + totalEntries + " IPs total. Hit GO to begin.");
                }
                catch (Exception ex)
                {
                    addLog("Error importing IPs " + ex.Message + "\r\n" + ex.StackTrace);
                    useImportedIPs = false;
                }

            }


        }



        #endregion

        #region core share enumeration
        private async Task<List<shareStruct>> _populateShareStructsTimeout(string ServerName)
        {
            List<shareStruct> retList = new List<shareStruct>();
            if (logLevel < LOG_LEVEL.ERROR)
            {
                var id = Task.CurrentId;
                Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " started populating shares on " + ServerName); });
            }

            var oNetworkCredential = getNetworkCredentials(ServerName);

                try
                {
                    //auth to server, we do want to timeout on discovery
                    using (new RemoteAccessHelper.NetworkConnection(@"\\" + ServerName, oNetworkCredential, true))
                    {
                        WinNetworking _getNetworkShares = new WinNetworking();

                        //get shares
                        WinNetworking.SHARE_INFO_1[] gnssi1 = _getNetworkShares.EnumNetShares(ServerName);
                        string currentShareName = "";

                        if (gnssi1 != null)
                        {
                            for (int i = 0; i < gnssi1.Length; i++)
                            {

                                shareStruct ss = new shareStruct()
                                {
                                    shareName = gnssi1[i].shi1_netname,
                                    ipAddressHostname = ServerName
                                };

                                currentShareName = ss.shareName;

                                //get dir acls
                                try
                                {
                                    if (_cancellationToken.IsCancellationRequested)
                                    {
                                        throw new OperationCanceledException();
                                    }
                                    var _dirPerm = Directory.GetAccessControl(@"\\" + ServerName + "\\" + ss.shareName);
                                    var _accessRules = _dirPerm.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));
                                    ss.permissionsList = _accessRules;

                                    foreach (FileSystemAccessRule fas in _accessRules)
                                    {
                                        if (fas.IdentityReference.ToString().ToLower() == "everyone")
                                        {
                                            ss.everyoneRights = fas.FileSystemRights;
                                            ss.everyoneCanRead = hasReadPermissions(fas.FileSystemRights);
                                        }
                                    }

                                    ss.currentUserCanRead = true;

                                }
                                catch (Exception ex)
                                {
                                    if (logLevel < LOG_LEVEL.INTERESTINGONLY)
                                    {
                                        Dispatcher.Invoke((Action)delegate { addLog("Error: " + ServerName + " (" + currentShareName + ")" + " - " + ex.Message); });
                                    }
                                }
                                retList.Add(ss);
                            }
                        }

                        if (logLevel < LOG_LEVEL.ERROR)
                        {
                            var id = Task.CurrentId;
                            Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " finished populating shares OK on " + ServerName); });
                        }
                    }
                }

                catch (OperationCanceledException)
                {
                    if (logLevel < LOG_LEVEL.ERROR)
                    {
                        Dispatcher.Invoke((Action)delegate { addLog("Error: " + ServerName + " - timed out"); });
                    }
                }

                catch (Exception ex)
                {
                    if (logLevel < LOG_LEVEL.INTERESTINGONLY)
                    {
                        Dispatcher.Invoke((Action)delegate { addLog("Error: " + ServerName + " - " + ex.Message); });
                    }
                }
            


            if (retList.Count > 0)
            {
                Dispatcher.Invoke((Action)delegate { addSharesToTreeview(retList); });
                //add it to the global list of all readable shares
                foreach (var res in retList)
                {
                    if (res.everyoneCanRead == true || res.currentUserCanRead == true)
                    {
                        if (all_readable_shares.ContainsKey(res.ipAddressHostname))
                        {
                            all_readable_shares[res.ipAddressHostname].Add(res);
                        }
                        else
                        {
                            all_readable_shares[res.ipAddressHostname] = new List<shareStruct>();
                            all_readable_shares[res.ipAddressHostname].Add(res);
                        }
                    }
                }
            }

            Dispatcher.Invoke((Action)delegate { pgbMain.Value += 1; });
            return retList;
        }


        //bug fix
        private static FileSystemRights MapGenericRightsToFileSystemRights(FileSystemRights OriginalRights)
        {
            FileSystemRights MappedRights = new FileSystemRights();
            Boolean blnWasNumber = false;
            if (Convert.ToBoolean(Convert.ToInt64(OriginalRights) & Convert.ToInt64(GenericRights.GENERIC_EXECUTE)))
            {
                MappedRights = MappedRights | FileSystemRights.ExecuteFile | FileSystemRights.ReadPermissions | FileSystemRights.ReadAttributes | FileSystemRights.Synchronize;
                blnWasNumber = true;
            }

            if (Convert.ToBoolean(Convert.ToInt64(OriginalRights) & Convert.ToInt64(GenericRights.GENERIC_READ)))
            {
                MappedRights = MappedRights | FileSystemRights.ReadAttributes | FileSystemRights.ReadData | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadPermissions | FileSystemRights.Synchronize;
                blnWasNumber = true;
            }
            if (Convert.ToBoolean(Convert.ToInt64(OriginalRights) & Convert.ToInt64(GenericRights.GENERIC_WRITE)))
            {
                MappedRights = MappedRights | FileSystemRights.AppendData | FileSystemRights.WriteAttributes | FileSystemRights.WriteData | FileSystemRights.WriteExtendedAttributes | FileSystemRights.ReadPermissions | FileSystemRights.Synchronize;
                blnWasNumber = true;
            }
            if (Convert.ToBoolean(Convert.ToInt64(OriginalRights) & Convert.ToInt64(GenericRights.GENERIC_ALL)))
            {
                MappedRights = MappedRights | FileSystemRights.FullControl;
                blnWasNumber = true;
            }

            if (blnWasNumber == false)
            {
                MappedRights = OriginalRights;
            }

            return MappedRights;
        }
        public static bool hasReadPermissions(FileSystemRights toRemainSilent)
        {
            toRemainSilent = MapGenericRightsToFileSystemRights(toRemainSilent);

            if (toRemainSilent.HasFlag(FileSystemRights.ReadData) ||
                toRemainSilent.HasFlag(FileSystemRights.Read) ||
                toRemainSilent.HasFlag(FileSystemRights.Modify) ||
                toRemainSilent.HasFlag(FileSystemRights.ListDirectory) ||
                toRemainSilent.HasFlag(FileSystemRights.ReadAndExecute) ||
                toRemainSilent.HasFlag(FileSystemRights.ReadExtendedAttributes) ||
                toRemainSilent.HasFlag(FileSystemRights.TakeOwnership) ||
                toRemainSilent.HasFlag(FileSystemRights.ChangePermissions) ||
                toRemainSilent.HasFlag(FileSystemRights.FullControl) ||
                toRemainSilent.HasFlag(FileSystemRights.DeleteSubdirectoriesAndFiles) ||
                toRemainSilent.HasFlag(FileSystemRights.Delete))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        #endregion

        #region core file/directory enumeration

        private bool getInterstingFileList(string sharepath)
        {
            try
            {
                List<string> finalInteresting = new List<string>();
                var oNetworkCredential = getNetworkCredentials(sharepath.Split('\\')[0]);
                //no need to timeout on long running tasko
                using (new RemoteAccessHelper.NetworkConnection(@"\\" + sharepath, oNetworkCredential, false))
                {
                    List<string> firstFileList = new List<string>();
                    //low hanging
                    if (logLevel < LOG_LEVEL.ERROR)
                    {
                        var id = Task.CurrentId;
                        Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " started searching interesting files (top dir only) on " + sharepath); });
                    }
                    try
                    {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                        firstFileList = Directory.EnumerateFiles(@"\\" + sharepath, "*.*", SearchOption.TopDirectoryOnly).ToList<string>();
                        if (firstFileList != null)
                        {
                            foreach (string file in firstFileList)
                            {
                                string fi = System.IO.Path.GetFileName(file);
                                isInteresting(fi, file);
                                //update gui label with file count
                                updateNumberofFilesProcessedLabel();

                                if (logLevel == LOG_LEVEL.DEBUG)
                                {
                                    var id = Task.CurrentId;
                                    Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " found file " + fi); });
                                }
                            }

                            if (logLevel < LOG_LEVEL.ERROR)
                            {
                                var id = Task.CurrentId;
                                Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " succsessfully finished searching interesting files (top dir only) on " + sharepath); });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (logLevel < LOG_LEVEL.INTERESTINGONLY)
                        {
                            Dispatcher.Invoke((Action)delegate { addLog("Error searching in " + sharepath + " - " + ex.Message); });
                        }
                    }





                    if (recursiveSearch == true)
                    {
                        if (logLevel < LOG_LEVEL.ERROR)
                        {
                            var id = Task.CurrentId;
                            Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " starting recursively searching for interesting files on " + sharepath); });
                        }


                        List<string> recursiveList = getAllFilesOnShare(sharepath);

                   

                        foreach (string file in recursiveList)
                        {
                            if (!firstFileList.Contains(file))
                            {
                                string fi = System.IO.Path.GetFileName(file);
                                isInteresting(fi, file);
                                //update gui label with file count
                                updateNumberofFilesProcessedLabel();
                            }
                            if (_cancellationToken.IsCancellationRequested)
                            {
                                throw new OperationCanceledException();
                            }
                        }

                        if (logLevel < LOG_LEVEL.ERROR)
                        {
                            var id = Task.CurrentId;
                            Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " finished recursively searching for interesting files on " + sharepath); });
                        }
                    }
                    Dispatcher.Invoke((Action)delegate { pgbMain.Value += 1; });
                    return true;
                }
            }

            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (logLevel < LOG_LEVEL.INTERESTINGONLY)
                {
                    Dispatcher.Invoke((Action)delegate { addLog("Error: failed to enumerate files on " + sharepath + " - " + ex.Message); });
                }
                return false;
            }
       
        }

        private bool getFileContentsList(string sharepath)
        {
            try
            {
                List<string> finalInteresting = new List<string>();
                var oNetworkCredential = getNetworkCredentials(sharepath.Split('\\')[0]);
                //no need to timeout on long running tasko
                using (new RemoteAccessHelper.NetworkConnection(@"\\" + sharepath, oNetworkCredential, false))
                {
                    List<string> firstFileList = new List<string>();
                    //low hanging
                    if (logLevel < LOG_LEVEL.ERROR)
                    {
                        var id = Task.CurrentId;
                        Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " started searching interesting files (top dir only) on " + sharepath); });
                    }
                    try
                    {
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException();
                        }

                        firstFileList = Directory.EnumerateFiles(@"\\" + sharepath, "*.*", SearchOption.TopDirectoryOnly).ToList<string>();
                        if (firstFileList != null)
                        {
                            foreach (string file in firstFileList)
                            {
                                if (_cancellationToken.IsCancellationRequested)
                                {
                                    throw new OperationCanceledException();
                                }
                                inspectFile(file);
                                //update gui label with file count
                                updateNumberofFilesProcessedLabel();

                                if (logLevel == LOG_LEVEL.DEBUG)
                                {
                                    var id = Task.CurrentId;
                                    Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " found file " + file); });
                                }
                            }

                            if (logLevel < LOG_LEVEL.ERROR)
                            {
                                var id = Task.CurrentId;
                                Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " succsessfully finished grepping files (top dir only) on " + sharepath); });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (logLevel < LOG_LEVEL.INTERESTINGONLY)
                        {
                            Dispatcher.Invoke((Action)delegate { addLog("Error searching in " + sharepath + " - " + ex.Message); });
                        }
                    }


                    if (recursiveSearch == true)
                    {
                        if (logLevel < LOG_LEVEL.ERROR)
                        {
                            var id = Task.CurrentId;
                            Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " starting recursively searching for greppable files on " + sharepath); });
                        }


                        List<string> recursiveList = getAllFilesOnShare(sharepath);
                        foreach (string file in recursiveList)
                        {
                            if (_cancellationToken.IsCancellationRequested)
                            {
                                throw new OperationCanceledException();
                            }

                            if (!firstFileList.Contains(file))
                            {
                                inspectFile(file);
                                //update gui label with file count
                                updateNumberofFilesProcessedLabel();
                            }
                        }

                        if (logLevel < LOG_LEVEL.ERROR)
                        {
                            var id = Task.CurrentId;
                            Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " finished recursively searching for interesting files on " + sharepath); });
                        }
                    }
                    Dispatcher.Invoke((Action)delegate { pgbMain.Value += 1; });
                    return true;
                }
            }

            catch (OperationCanceledException)
            {
                throw;
            }

            catch (Exception ex)
            {
                if (logLevel < LOG_LEVEL.INTERESTINGONLY)
                {
                    Dispatcher.Invoke((Action)delegate { addLog("Error: failed to enumerate files on " + sharepath + " - " + ex.Message); });
                }
                return false;
            }
        }

        private bool inspectFile(string filePath)
        {
            try
            {
                FileInfo fi = new FileInfo(filePath);
                if (fi.Length <= MAX_FILESIZE * 1024)
                {
                    if (includeBinaryFiles == false)
                    {

                        binaryHelper bh = new binaryHelper();
                        Encoding e;
                        try
                        {
                            if (bh.IsText(out e, filePath, 100) == true)
                            {
                                string line;
                                StreamReader sr = new StreamReader(filePath, e);
                                if (logLevel < LOG_LEVEL.INFO)
                                {
                                    var threadId = Task.CurrentId;
                                    Dispatcher.Invoke((Action)delegate { addLog("Thread " + threadId + " began inspecting " + filePath); });
                                }
                                while ((line = sr.ReadLine()) != null)
                                {
                                    isFilterMatch(line, filePath);
                                }

                                sr.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            if (logLevel < LOG_LEVEL.INFO)
                            {
                                Dispatcher.Invoke((Action)delegate { addLog("Failed to open " + filePath + " for reading " + ex.Message); });
                            }
                        }
                    } //end binary files only

                    else //include binary files
                    {
                        try
                        {
                            string line;
                            StreamReader sr = new StreamReader(filePath);
                            if (logLevel < LOG_LEVEL.INFO)
                            {
                                var threadId = Task.CurrentId;
                                Dispatcher.Invoke((Action)delegate { addLog("Thread " + threadId + " began inspecting " + filePath); });
                            }
                            while ((line = sr.ReadLine()) != null)
                            {
                                isFilterMatch(line, filePath);
                            }

                            sr.Close();
                        }
                        catch (Exception ex)
                        {
                            if (logLevel < LOG_LEVEL.INFO)
                            {
                                Dispatcher.Invoke((Action)delegate { addLog("Failed to open " + filePath + " for reading " + ex.Message); });
                            }
                        }
                    }//end all files (including binary) 

                    if (logLevel < LOG_LEVEL.INFO)
                    {
                        var threadId = Task.CurrentId;
                        Dispatcher.Invoke((Action)delegate { addLog("Thread " + threadId + " finished inspecting " + filePath); });
                    }

                }//end small files only
            }

            catch (Exception ex)
            {
                if (logLevel < LOG_LEVEL.INFO)
                {
                    Dispatcher.Invoke((Action)delegate { addLog("Failed to get file info for " + filePath + " - " + ex.Message); });
                }
            }
            updateNumberofFilesProcessedLabel();
            return true;
        }
                  
    
        //needs updating
        private List<string> getAllFilesOnShare(string sharepath)
        {
            List<string> recursiveList = new List<string>();
            string server = sharepath.Split('\\')[0];
            string share = sharepath.Split('\\')[1];

            if (!all_readable_files.ContainsKey(server))
            {
                all_readable_files.TryAdd(server, new Dictionary<string, List<string>>());
            }
                if (!all_readable_files[server].ContainsKey(share))
                {
                    Dispatcher.Invoke((Action)delegate { addLog("Enumerating all files on " + sharepath + " this may take a while..."); });
                    recursiveList = getDirectoryFilesRecursive(@"\\" + sharepath).ToList<string>();
                    Dispatcher.Invoke((Action)delegate { addLog("Finished enumerating files on " + sharepath); });
                    all_readable_files[server][share] = recursiveList;

                }
       
            else   //do not need to enumerate all files if we have a list already
            {
                if (all_readable_files[server].ContainsKey(share))
                {
                    recursiveList = all_readable_files[server][share];
                }
            }

            return recursiveList;

        }

        private IEnumerable<string> getDirectoryFilesRecursive(string path)
        {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0 && !_cancellationToken.IsCancellationRequested)
            {

                path = queue.Dequeue();

                if (logLevel == LOG_LEVEL.DEBUG)
                {
                    var id = Task.CurrentId;
                    Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " found directory " + path); });
                }

                try
                {
              
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                        foreach (string subDir in Directory.GetDirectories(path))
                        {
                            queue.Enqueue(subDir);
                        }
                    
                }
                catch (Exception ex)
                {
                    if (logLevel < LOG_LEVEL.ERROR)
                    {
                        Dispatcher.Invoke((Action)delegate { addLog("Permission denied for shares in directory " + path + " - " + ex.Message); });
                    }
                }
                string[] files = null;
                try
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                    {
                        files = Directory.GetFiles(path);

                        if (logLevel == LOG_LEVEL.DEBUG)
                        {
                            var id = Task.CurrentId;
                            foreach (string fi in files)
                            {
                                Dispatcher.Invoke((Action)delegate { addLog("Thread " + id + " found path " + fi); });
                            }
                        }
                    }
                }

                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (logLevel < LOG_LEVEL.ERROR)
                    {
                        Dispatcher.Invoke((Action)delegate { addLog("Permission denied for shares in directory " + path + " - " + ex.Message); });
                    }
                }

                if (files != null)
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    for (int i = 0; i < files.Length; i++)
                    {
                        yield return files[i];
                    }
                }
            }
        }

        #endregion

        #region misc

        /// <summary>
        /// checks to see if a given filename is "interesting" or not, doesnt return a value as everything is stored in the results pane anyway
        /// </summary>
        /// <param name="shortFileName"></param>
        /// <param name="filePath"></param>
        private void isInteresting(string shortFileName, string filePath)
        {

     
            foreach (string interesting in interestingFileList)
            {

                if (shortFileName == interesting)
                {
                    addToResultsList(filePath, shortFileName, "filename directly matches rule  (" + interesting + ")");
                    Dispatcher.Invoke((Action)delegate { addLog("Interesting file found - " + shortFileName + " (" + shortFileName + ")"); });
                    return;
                }

                string lowered = shortFileName.ToLower();
                if (lowered == interesting)
                {
                    addToResultsList(filePath, shortFileName, "filename matches rule  (" + interesting + ")");
                    Dispatcher.Invoke((Action)delegate { addLog("Interesting file found - " + shortFileName + " (" + shortFileName + ")"); });
                    return;
                }

             
                //if (shortFileName.Contains("."))
                //{
                //    if (System.IO.Path.GetFileNameWithoutExtension(lowered) == interesting)
                //    {
                //        return true;
                //    }
                //}

                //anything.file
                if (interesting.StartsWith("*."))
                {
                    if (System.IO.Path.GetExtension(lowered) == interesting.TrimStart('*'))
                    {
                        addToResultsList(filePath, shortFileName, "extension rule matched (" + interesting + ")");
                        Dispatcher.Invoke((Action)delegate { addLog("Interesting file found - " + shortFileName + " (" + shortFileName + ")"); });
                        return;
                    }
                }

                //filename.anything
                else if (interesting.EndsWith(".*"))
                {
                    string aa = System.IO.Path.GetFileNameWithoutExtension(lowered);
                    string bb = interesting.TrimEnd('*').TrimEnd('.');
                    if (System.IO.Path.GetFileNameWithoutExtension(lowered) == interesting.TrimEnd('*').TrimEnd('.'))
                    {
                        addToResultsList(filePath, shortFileName, "wildcard extension rule matched (" + interesting + ")");
                        Dispatcher.Invoke((Action)delegate { addLog("Interesting file found - " + shortFileName + " (" + shortFileName + ")"); });
                        return;
                    }
                }

                //regex
                else if (interesting.StartsWith("###"))
                {
                    try
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(shortFileName, interesting.TrimStart('#'), System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            addToResultsList(filePath, shortFileName, "regex matched (" + interesting.TrimStart('#') + ")");
                            Dispatcher.Invoke((Action)delegate { addLog("Interesting file found - " + shortFileName + " (" + shortFileName + ")"); });
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (logLevel <= LOG_LEVEL.ERROR)
                        {
                            Dispatcher.Invoke((Action)delegate { addLog("Regex parsing failed on interesting name comparison, file - " + shortFileName + " regex parsed " + interesting + "(" + ex.Message + ")"); });
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// checks to see if a line within a file is a match or not. doesn't return a value as it is stored within the results pane anyway..
        /// </summary>
        /// <param name="line"></param>
        /// <param name="path"></param>
        private void isFilterMatch(string line, string path)
        {
            string lowered = line.ToLower();
            foreach (string fileFilter in fileContentsFilters)
            {
            
                if (fileFilter.StartsWith("###"))
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(lowered, fileFilter.TrimStart('#').ToLower()) == true)
                    {
                        Dispatcher.Invoke((Action)delegate { addLog("Interesting file found - " + System.IO.Path.GetFileName(path) + " (" + path + ") matches regex filter rule: " + fileFilter); });
                        addToResultsList(path, System.IO.Path.GetFileName(path), "contents regex matched (" + fileFilter.TrimStart('#') + ")");
                    }
                }
                else if (lowered.Contains(fileFilter.ToLower()))
                {
                    Dispatcher.Invoke((Action)delegate { addLog("Interesting file found - " + System.IO.Path.GetFileName(path) + " (" + path + ") matches filter rule: " + fileFilter); });
                    addToResultsList(path, System.IO.Path.GetFileName(path), "file contents matched " + fileFilter);
                }
            }
        }

        /// <summary>
        /// get network credentials, depending on if we need to auth locally or not..
        /// </summary>
        /// <returns></returns>
        private static NetworkCredential getNetworkCredentials(string ServerName)
        {
            var oNetworkCredential = new System.Net.NetworkCredential();
            if (AUTHLOCALLY == true)
            {
                oNetworkCredential.UserName = ServerName + USERNAME.TrimStart('.');
                oNetworkCredential.Password = PASSSWORD;
            }
            else
            {
                oNetworkCredential.UserName = USERNAME;
                oNetworkCredential.Password = PASSSWORD;
            }

            return oNetworkCredential;
        }
        
        private void resetTokens()
        {
            _cancellationToken = new CancellationTokenSource();
            _parallelOption = new ParallelOptions { MaxDegreeOfParallelism = 30, CancellationToken = _cancellationToken.Token };
        }

        #endregion

     


    }




}



    

       







