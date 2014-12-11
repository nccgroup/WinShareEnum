using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using System.IO;
using System.Collections.Specialized;
using System.Reflection;
using System.Diagnostics;
using System.Security.Principal;

namespace WinShareEnum
{
    public class IPRange
    {
        public IPRange(string ipRange)
        {
            if (ipRange == null)
                throw new ArgumentNullException();

            if (!TryParseCIDRNotation(ipRange) && !TryParseSimpleRange(ipRange))
                throw new ArgumentException();
        }

        public IList<IPAddress> GetAllIP()
        {
            int capacity = 1;
            for (int i = 0; i < 4; i++)
                capacity *= endIP[i] - beginIP[i] + 1;

            List<IPAddress> ips = new List<IPAddress>(capacity);
            for (int i0 = beginIP[0]; i0 <= endIP[0]; i0++)
            {
                for (int i1 = beginIP[1]; i1 <= endIP[1]; i1++)
                {
                    for (int i2 = beginIP[2]; i2 <= endIP[2]; i2++)
                    {
                        for (int i3 = beginIP[3]; i3 <= endIP[3]; i3++)
                        {
                            ips.Add(new IPAddress(new byte[] { (byte)i0, (byte)i1, (byte)i2, (byte)i3 }));
                        }
                    }
                }
            }

            return ips;
        }

        /// <summary>
        /// Parse IP-range string in CIDR notation.
        /// For example "12.15.0.0/16".
        /// </summary>
        /// <param name="ipRange"></param>
        /// <returns></returns>
        private bool TryParseCIDRNotation(string ipRange)
        {
            string[] x = ipRange.Split('/');

            if (x.Length != 2)
                return false;

            byte bits = byte.Parse(x[1]);
            uint ip = 0;
            String[] ipParts0 = x[0].Split('.');
            for (int i = 0; i < 4; i++)
            {
                ip = ip << 8;
                ip += uint.Parse(ipParts0[i]);
            }

            byte shiftBits = (byte)(32 - bits);
            uint ip1 = (ip >> shiftBits) << shiftBits;

            if (ip1 != ip) // Check correct subnet address
                return false;

            uint ip2 = ip1 >> shiftBits;
            for (int k = 0; k < shiftBits; k++)
            {
                ip2 = (ip2 << 1) + 1;
            }

            beginIP = new byte[4];
            endIP = new byte[4];

            for (int i = 0; i < 4; i++)
            {
                beginIP[i] = (byte)((ip1 >> (3 - i) * 8) & 255);
                endIP[i] = (byte)((ip2 >> (3 - i) * 8) & 255);
            }

            return true;
        }

        /// <summary>
        /// Parse IP-range string "12.15-16.1-30.10-255"
        /// </summary>
        /// <param name="ipRange"></param>
        /// <returns></returns>
        private bool TryParseSimpleRange(string ipRange)
        {
            String[] ipParts = ipRange.Split('.');

            beginIP = new byte[4];
            endIP = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                string[] rangeParts = ipParts[i].Split('-');

                if (rangeParts.Length < 1 || rangeParts.Length > 2)
                    return false;

                beginIP[i] = byte.Parse(rangeParts[0]);
                endIP[i] = (rangeParts.Length == 1) ? beginIP[i] : byte.Parse(rangeParts[1]);
            }

            return true;
        }

        private byte[] beginIP;
        private byte[] endIP;
    }

    /// <summary>
    /// impersonation helper
    /// </summary>
    public class RemoteAccessHelper
    {
        public class NetworkConnection : IDisposable
        {
            string _networkName;

            public NetworkConnection(string networkName, NetworkCredential credentials, bool timeout)
            {
                _networkName = networkName;

                var netResource = new NetResource()
                {
                    Scope = ResourceScope.GlobalNetwork,
                    ResourceType = ResourceType.Disk,
                    DisplayType = ResourceDisplaytype.Share,
                    RemoteName = networkName
                };

                try
                {
                    var result = 0;

                    if (timeout == true)
                    {
                        var tokenSource = new CancellationTokenSource();
                        CancellationToken token = tokenSource.Token;
                        int timeOut = MainWindow.TIMEOUT;

                        var task = Task.Factory.StartNew(() =>
                            {
                                result = WNetAddConnection2(
                                netResource,
                                credentials.Password,
                                credentials.UserName,
                                0);

                            }, token);

                        if (!task.Wait(timeOut, token))
                            throw new Win32Exception("The request timed out.");

                    }
                    else
                    {

                        result = WNetAddConnection2(
                        netResource,
                        credentials.Password,
                        credentials.UserName,
                        0);

                    }
                    if (result != 0)
                    {
                        throw new Win32Exception(result);
                    }
                }

                catch (Exception ex)
                {
                    throw new Exception("Error connecting to remote share: " + ex.Message);
                }
            }

            ~NetworkConnection()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                WNetCancelConnection2(_networkName, 0, true);
            }

            [DllImport("mpr.dll")]
            private static extern int WNetAddConnection2(NetResource netResource,
                string password, string username, int flags);

            [DllImport("mpr.dll")]
            private static extern int WNetCancelConnection2(string name, int flags,
                bool force);
        }

        [StructLayout(LayoutKind.Sequential)]
        public class NetResource
        {
            public ResourceScope Scope;
            public ResourceType ResourceType;
            public ResourceDisplaytype DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        public enum ResourceScope : int
        {
            Connected = 1,
            GlobalNetwork,
            Remembered,
            Recent,
            Context
        };

        public enum ResourceType : int
        {
            Any = 0,
            Disk = 1,
            Print = 2,
            Reserved = 8,
        }

        public enum ResourceDisplaytype : int
        {
            Generic = 0x0,
            Domain = 0x01,
            Server = 0x02,
            Share = 0x03,
            File = 0x04,
            Group = 0x05,
            Network = 0x06,
            Root = 0x07,
            Shareadmin = 0x08,
            Directory = 0x09,
            Tree = 0x0a,
            Ndscontainer = 0x0b
        }
    }

    /// <summary>
    /// pinvoke wrapper
    /// </summary>
    public class WinNetworking
    {

        #region External Calls
        [DllImport("Netapi32.dll", SetLastError = true)]
        static extern int NetApiBufferFree(IntPtr Buffer);
        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetShareEnum(
             StringBuilder ServerName,
             int level,
             ref IntPtr bufPtr,
             uint prefmaxlen,
             ref int entriesread,
             ref int totalentries,
             ref int resume_handle
             );
        #endregion
        #region External Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHARE_INFO_1
        {
            public string shi1_netname;
            public uint shi1_type;
            public string shi1_remark;
            public SHARE_INFO_1(string sharename, uint sharetype, string remark)
            {
                this.shi1_netname = sharename;
                this.shi1_type = sharetype;
                this.shi1_remark = remark;
            }
            public override string ToString()
            {
                return shi1_netname;
            }
        }
        #endregion

        const uint MAX_PREFERRED_LENGTH = 0xFFFFFFFF;
        const int NERR_Success = 0;


        //nb. share info 1 not 502/3 due to privilege issues :-(
        public SHARE_INFO_1[] EnumNetShares(string Server)
        {
            List<SHARE_INFO_1> ShareInfos = new List<SHARE_INFO_1>();
            int entriesread = 0;
            int totalentries = 0;
            int resume_handle = 0;
            int nStructSize = Marshal.SizeOf(typeof(SHARE_INFO_1));
            IntPtr bufPtr = IntPtr.Zero;
            StringBuilder server = new StringBuilder(Server);

            int ret = NetShareEnum(server, 1, ref bufPtr, MAX_PREFERRED_LENGTH, ref entriesread, ref totalentries, ref resume_handle);
            if (ret == NERR_Success)
            {
                IntPtr currentPtr = bufPtr;
                for (int i = 0; i < entriesread; i++)
                {
                    if (MainWindow._cancellationToken.IsCancellationRequested == true)
                    {
                        throw new OperationCanceledException();
                    }
                    SHARE_INFO_1 shi1 = (SHARE_INFO_1)Marshal.PtrToStructure(currentPtr, typeof(SHARE_INFO_1));
                    ShareInfos.Add(shi1);


                    currentPtr = new IntPtr(currentPtr.ToInt32() + nStructSize);
                }
                NetApiBufferFree(bufPtr);
                return ShareInfos.ToArray();
            }
            else
            {
                //ShareInfos.Add(new SHARE_INFO_1("ERROR=" + ret.ToString(), 10, string.Empty));
                //return ShareInfos.ToArray();
                return null;
            }
        }



    }

    /// <summary>
    /// saves to file, nb this also updates the program internal lists (interestingFileList, fileContentsFilters)
    /// </summary>
    public class persistance
    {
        public persistance()
        {
            if (Settings.Default.interestingFileNameRules == null)
            {
                Settings.Default.interestingFileNameRules = new StringCollection();
                foreach (string s in MainWindow.interestingFileList)
                {
                    Settings.Default.interestingFileNameRules.Add(s);
                }
                Settings.Default.Save();
            }

            if (Settings.Default.FileContentRules == null)
            {
                Settings.Default.FileContentRules = new StringCollection();
                foreach (string s in MainWindow.fileContentsFilters)
                {
                    Settings.Default.FileContentRules.Add(s);
                }

                Settings.Default.Save();
            }
        }

        #region get
        public static List<string> getInterestingFiles()
        {
            return Settings.Default.interestingFileNameRules.Cast<string>().ToList();
        }

        public static List<string> getFileContentRules()
        {
            return Settings.Default.FileContentRules.Cast<string>().ToList();
        }

        #endregion

        #region set

        public static void saveInterestingRule(string interesting)
        {
            Settings.Default.interestingFileNameRules.Add(interesting);
            MainWindow.interestingFileList.Add(interesting);
            Settings.Default.Save();
        }

        public static void saveFileContentRule(string fileContent)
        {
            Settings.Default.FileContentRules.Add(fileContent);
            MainWindow.fileContentsFilters.Add(fileContent);
            Settings.Default.Save();
        }
        #endregion

        #region delete
        public static void deleteInterestingRule(string interesting)
        {
            Settings.Default.interestingFileNameRules.Remove(interesting);
            MainWindow.interestingFileList.Remove(interesting);
            Settings.Default.Save();
        }

        public static void deleteFileContentRule(string fileContent)
        {
            Settings.Default.FileContentRules.Remove(fileContent);
            MainWindow.fileContentsFilters.Remove(fileContent);
            Settings.Default.Save();
        }

        #endregion

    }

    public class binaryHelper
    {


        /// <summary>
        /// Detect if a file is text and detect the encoding.
        /// </summary>
        /// <param name="encoding">
        /// The detected encoding.
        /// </param>
        /// <param name="fileName">
        /// The file name.
        /// </param>
        /// <param name="windowSize">
        /// The number of characters to use for testing.
        /// </param>
        /// <returns>
        /// true if the file is text.
        /// </returns>
        public bool IsText(out Encoding encoding, string fileName, int windowSize)
        {
            using (var fileStream = File.OpenRead(fileName))
            {
                var rawData = new byte[windowSize];
                var text = new char[windowSize];
                var isText = true;

                // Read raw bytes
                var rawLength = fileStream.Read(rawData, 0, rawData.Length);
                fileStream.Seek(0, SeekOrigin.Begin);

                // Detect encoding correctly (from Rick Strahl's blog)
                // http://www.west-wind.com/weblog/posts/2007/Nov/28/Detecting-Text-Encoding-for-StreamReader
                if (rawData[0] == 0xef && rawData[1] == 0xbb && rawData[2] == 0xbf)
                {
                    encoding = Encoding.UTF8;
                }
                else if (rawData[0] == 0xfe && rawData[1] == 0xff)
                {
                    encoding = Encoding.Unicode;
                }
                else if (rawData[0] == 0 && rawData[1] == 0 && rawData[2] == 0xfe && rawData[3] == 0xff)
                {
                    encoding = Encoding.UTF32;
                }
                else if (rawData[0] == 0x2b && rawData[1] == 0x2f && rawData[2] == 0x76)
                {
                    encoding = Encoding.UTF7;
                }
                else
                {
                    encoding = Encoding.Default;
                }

                // Read text and detect the encoding
                using (var streamReader = new StreamReader(fileStream))
                {
                    streamReader.Read(text, 0, text.Length);
                    streamReader.Close();
                }

                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, encoding))
                    {
                        // Write the text to a buffer
                        streamWriter.Write(text);
                        streamWriter.Flush();

                        // Get the buffer from the memory stream for comparision
                        var memoryBuffer = memoryStream.GetBuffer();

                        // Compare only bytes read
                        for (var i = 0; i < rawLength && isText; i++)
                        {
                            isText = rawData[i] == memoryBuffer[i];
                        }
                    }
                }

                return isText;
            }
        }




    }

    public class updates
    {

        public static double getCurrentVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = fvi.FileVersion;
            return double.Parse(version);
        }

        public static List<string> getInterestingFileUpdates()
        {
            return readFromSite(new Uri("https://raw.githubusercontent.com/nccgroup/WinShareEnum/master/Info/interestingFiles.txt"));
            
        }

        public static List<string>getFileFilterUpdates()
        {
            return readFromSite(new Uri("https://raw.githubusercontent.com/nccgroup/WinShareEnum/master/Info/filterRules.txt"));
        }
        
        public static double getLatestVersion()
        {
            return double.Parse(readFromSite(new Uri("https://raw.githubusercontent.com/nccgroup/WinShareEnum/master/Info/version.txt"))[0]);                    
        }
        

        public static string downloadUpdate(double newestVersion)
        {
            WebClient client = new WebClient();
            client.Proxy = null;
            string filePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\WinShareEnum-" + newestVersion.ToString() + ".exe";

            if (File.Exists(filePath))
            {
                return filePath;
            }

            client.DownloadFile("https://github.com/nccgroup/WinShareEnum/raw/master/Info/WinShareEnum.exe", filePath);
            return filePath;
        }


        private static List<string> readFromSite(Uri url)
        {
            WebClient client = new WebClient();
            client.Proxy = null;
            return client.DownloadString(url).Split('\n').ToList<string>(); ;
        }
    }
}