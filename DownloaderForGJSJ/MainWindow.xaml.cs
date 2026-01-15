using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VideoDL_m3u8;
using VideoDL_m3u8.Extensions;

namespace DownloaderForGJSJ
{
    /// <summary>
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")] public static extern int MessageBoxTimeoutA(IntPtr hWnd, string msg, string Caps, int type, int Id, int time);

        /////////////////////////////////////////////////////
        ///1.内部类
        #region
        [Serializable()]
        class DownloadPackage
        {
            public bool IsSaving { get; set; }
            public bool IsSaveComplete { get; set; }
            public double SaveProgress { get; set; }
            public string Address = "";
            //public long ReceivedFileSize { get; set; }

            public long speedReceived { get; set; }//20240508
            public long bytesReceived { get; set; }//20240508
            public long totalBytesToReceive { get; set; }//20240508

            //public long TotalFileSize { get; set; }
            public string FileName = "";
        }
        [Serializable()]
        class WebSite
        {
            public string SelectorForUrl1 { get; set; }//第一次解析
            public string SelectorForUrl2 { get; set; }//第二次解析，两步解析的第二步使用；一步解析用

            public string FileExtension { get; set; }
            public int ThreadNum { get; set; }
            public bool UseProxy { get; set; }
            public string ProxyHost = PROXY_HOST_DEFAULT;
            public int ProxyPort = PROXY_PORT_DEFAULT;

            public List<string>? UserAgents = null;
            public WebSite(string selectorForUrl1, string selectorForUrl2, string fileExtension, int ThreadNum, bool UseProxy, string ProxyHost, int ProxyPort, List<string>? userAgents)
            {
                this.SelectorForUrl1 = selectorForUrl1;
                this.SelectorForUrl2 = selectorForUrl2;
                this.FileExtension = fileExtension;
                this.ThreadNum = ThreadNum;
                this.UseProxy = UseProxy;
                this.ProxyHost = ProxyHost;
                this.ProxyPort = ProxyPort;
                UserAgents = userAgents;
            }
        }

        [Serializable()]
        class UrlItem
        {
            public string Title { get; set; }
            public string Url { get; set; }

            public UrlItem(string title, string url)
            {
                this.Title = title;
                this.Url = url;
            }
        }

        //////////////////////////////////////////
        [Serializable()]
        class WebSiteDownloadHistory
        {
            public string WebSiteName { get; set; }
            public string CurrentTargetUrl { get; set; }//干净世界时保存当前解析的连接，如果有下载任务未完成需保留下载历史时
            public List<DownloadPackage> DownloadPackages { get; set; }

            public WebSiteDownloadHistory(string webSiteName, List<DownloadPackage> downloadPackages, string currentTargetUrl = "")
            {
                WebSiteName = webSiteName;
                DownloadPackages = downloadPackages;
                CurrentTargetUrl = currentTargetUrl;
            }
        }
        //////////////////////////////////////////
        [Serializable()]
        class GjsjM3u8Json
        {
            public string contentUrl { get; set; }

            public GjsjM3u8Json(string contentUrl, string name)
            {
                this.contentUrl = contentUrl;
            }
        }
        [Serializable()]
        class VideoUrl
        {
            public string name { get; set; }
            public string resolution { get; set; }
            public string url { get; set; }
            public VideoUrl(string name, string resolution, string url)
            {
                this.name = name;
                this.resolution = resolution;
                this.url = url;
            }
            public override string ToString()
            {
                return $"{name} [{resolution}]: {url}";
            }
        }
        [Serializable()]
        class GjsjVideoDownloadFileItem
        {
            public long byteRangeOffset;
            public long byteRangeLength;
            public string lastVideoName;
            public string videoNameSaved;
            public GjsjVideoDownloadFileItem(long byteRangeOffset, long byteRangeLength, string lastVideoName, string videoNameSaved = "")
            {
                this.byteRangeOffset = byteRangeOffset;
                this.byteRangeLength = byteRangeLength;
                this.lastVideoName = lastVideoName;
                this.videoNameSaved = videoNameSaved;
            }
            public string ToRange()
            {
                return $"{byteRangeOffset}-{byteRangeLength}";
            }
        }

        //20250115 for new changes in the website
        [Serializable()]
        class GjsjM3u8Json20250115
        {
            public Props props { get; set; }

            public GjsjM3u8Json20250115(Props props, string name)
            {
                this.props = props;
            }
        }

        [Serializable()]
        class Props
        {
            public PageProps pageProps { get; set; }

            public Props(PageProps pageProps)
            {
                this.pageProps = pageProps;
            }
        }

        [Serializable()]
        class PageProps
        {
            public Video video { get; set; }
            public PageProps(Video video)
            {
                this.video = video;
            }
        }

        [Serializable()]
        class Video
        {
            public string video_Url { get; set; }
            public Video(string videoUrl)
            {
                this.video_Url = videoUrl;
            }

        }
        //////////////////////////////////////////
        [Serializable()]
        class DownloadItem : INotifyPropertyChanged
        {
            public int id { get; set; }
            public int DisplayId { get { return id + 1; } }
            /// <summary>
            /// TS文件专用，下载“干净世界”文件
            /// </summary>
            public int tsPosistion { get; set; } //20231106 TS文件下载，每个文件按照读取的顺序作为文件名，来保存文件，也就是说，第一填表时就确定的顺序，以后不改
            public int tsTotalNum { get; set; } //20231106 TS文件总数
            ////// 
            public string fileName { get; set; }
            public string intermediateUrl { get; set; }

            private string _downloadUrl = "";
            public string downloadUrl
            {
                get { return _downloadUrl; }
                set
                {
                    if (_downloadUrl == value)
                        return;
                    _downloadUrl = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("downloadUrl"));
                    }
                }
            }

            public string folderPath;
            public string fullFileName;

            private int _downloadProgress;

            //80%
            public int downloadProgress
            {
                get { return _downloadProgress; }
                set
                {
                    if (_downloadProgress == value)
                        return;
                    _downloadProgress = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("downloadProgress"));
                    }
                }
            }

            private string _downloadSpeed = "";

            // 200KB/s
            public string downloadSpeed
            {
                get { return _downloadSpeed; }
                set
                {
                    if (_downloadSpeed == value)
                        return;
                    _downloadSpeed = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("downloadSpeed"));
                    }
                }
            }

            public long speedReceived = 0;//20240508
            public long bytesReceived = 0;//20240508
            public long totalBytesToReceive = 0;//20240508

            private string _fileSize = "";
            // 1.2MB/18MB
            public string fileSize
            {
                get { return _fileSize; }
                set
                {
                    if (_fileSize == value)
                        return;
                    _fileSize = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("fileSize"));
                    }
                }
            }

            public bool downloadResult; // OK

            public event PropertyChangedEventHandler? PropertyChanged;

            public DownloadItem(int id, string fileName, string fileUrl, string targetUrl, string folderPath = "", int tsPosistion = 0, int tsTotalNum = 0, bool downloadResult = false)
            {
                this.id = id;
                this.tsPosistion = tsPosistion;
                this.tsTotalNum = tsTotalNum;
                this.fileName = fileName.Trim();
                this.intermediateUrl = fileUrl;
                this.downloadUrl = targetUrl;
                this.downloadResult = downloadResult;//20240227
                this.folderPath = string.IsNullOrWhiteSpace(folderPath) ? DOWNLOAD_PATH : folderPath;
                this.fullFileName = this.folderPath + "\\" + fileName;//干净世界的单独设置，避免与其它网站的冲突
                setDownloadInfo(0, "0.0KB/s", "0B", false);
            }

            public void setDownloadInfo(int downloadProgress, string downloadSpeed, string fileSize,/* string downloadSize, */bool downloadResult)
            {
                this.downloadProgress = downloadProgress;
                this.downloadSpeed = downloadSpeed;
                this.fileSize = fileSize;
                //this.downloadSize = downloadSize;
                this.downloadResult = downloadResult;
            }

            override
            public string ToString()
            {
                return string.Format("id = {0}[{8:D3}/{9:D3}], fileName = {1}, fileUrl = {2}, targetUrl = {3}, downloadProgress = {4}, downloadSpeed = {5}, fileSize = {6}, downloadResult = {7}", id, fileName, intermediateUrl, downloadUrl, downloadProgress, downloadSpeed, fileSize, downloadResult, tsPosistion, tsTotalNum);
            }
        }

        public class ObservableTaskProgress<T> : INotifyPropertyChanged
        {
            private T? _taskProgress;
            public T? TaskProgress
            {
                get { return _taskProgress; }
                set
                {
                    _taskProgress = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("TaskProgress"));//"Value"
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class ObservableLastColumnWidth : INotifyPropertyChanged
        {
            private double _lastColumnWidth;
            public double LastColumnWidth
            {
                get { return _lastColumnWidth; }
                set
                {
                    _lastColumnWidth = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("LastColumnWidth"));//"Value"
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        class M3u8Url
        {
            public string title { get; set; }
            public string url { get; set; }

            public M3u8Url(string title, string url)
            {
                this.title = title;
                this.url = url;
            }
            public override string ToString()
            {
                return $"title={title}, url={url}";
            }
        }


        enum AppTask
        {
            TASK_FETCH_DOWNLOAD_URL,
            TASK_DOWNLOAD,
            TASK_MERGE_TS_FILES
        }

        enum NewDownloadStateOfGjsj
        {
            STATE_VIDEO_OK,
            STATE_AUDIO_OK,
            STATE_VIDEO_AUDIO_OK,
            STATE_NONE
        }

        class ProxyState
        {
            public bool UseProxy;
            public string ProxyHost;
            public int ProxyPort;
            public bool IsProxyChanged;
            public ProxyState(bool useProxy = true, string proxyHost = PROXY_HOST_DEFAULT, int proxyPort = PROXY_PORT_DEFAULT, bool isProxyChanged = false)
            {
                UseProxy = useProxy;
                ProxyHost = proxyHost;
                ProxyPort = proxyPort;
                IsProxyChanged = isProxyChanged;
            }
        }
        #endregion

        /////////////////////////////////////////////////////
        ///2.参量
        #region
        private readonly List<string> EMPTY_STRING_LIST = new List<string>();

        private static string APP_PATH = Directory.GetCurrentDirectory();
        private string SETTINGS_JSON_FILE = APP_PATH + @"\settings.json";
        private string DOWNLOAD_HISTORY_JSON_FILE = APP_PATH + @"\download_history.json";
        private string FFMPEG_FILE = APP_PATH + @"\ffmpeg.exe";
        private string TMP_FILE = APP_PATH + @"\tmp";
        private string README_FILE = APP_PATH + @"\readme.md";
        ObservableCollection<DownloadItem> downloadItemList = new ObservableCollection<DownloadItem>();
        List<DownloadItem> downloadList = new List<DownloadItem>();
        ObservableTaskProgress<double> taskProgress = new ObservableTaskProgress<double>();
        private const string USER_AGENT_DEFAULT = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.99 Safari/537.36";
        private const string PROXY_HOST_DEFAULT = "127.0.0.1";
        private const int PROXY_PORT_DEFAULT = 8580;

        private const string WEB_SITE_NAME = "干净世界";

        WebSite? webSite;
        private string oldTaskTarget = "";
        WebSiteDownloadHistory? webSiteDownloadHistory;

        //private int oldCmbPageValueIndex = -1;
        //private bool oldUseProxy = false;
        //private int oldProxyPort = 0;
        private ProxyState proxyState = new ProxyState();

        private string userAgent = USER_AGENT_DEFAULT;

        bool test = false; //!!!

        //20220605
        private static string DOWNLOAD_PATH = APP_PATH + @"\下载";
        private readonly int DEFAULT_THREAD_NUM = 3;
        private readonly List<int> threadNums = new List<int>() { 1, 2, 3, 6, 9 };
        private AtomicBoolean abFetchDownloadUrl = new AtomicBoolean(false);
        private AtomicBoolean abStartDownload = new AtomicBoolean(false);
        private AtomicBoolean abMergeTSFiles = new AtomicBoolean(false);
        private object obj = new object();
        private HashSet<string> errors = new HashSet<string>();

        //20231123
        CancellationTokenSource cancellationToken = new CancellationTokenSource();

        //20240222
        VideoDL_m3u8.Parser.MasterPlaylist MasterPlaylist = new();//20240717 添加new()
        public List<string> GjsjVideoQualityList = new List<string>();
        public List<string> GjsjAudioQualityList = new List<string>();

        private readonly string GJSJ_MEDIA_PART_VIDEO = "【视频】";
        private readonly string GJSJ_MEDIA_PART_AUDIO = "【音频】";
        //ObservableIsSelectionEnabled IsSelectionEnabled = new ObservableIsSelectionEnabled();
        private readonly int GJSJ_VALID_FILE_SIZE = 30 * 1024; //30KB
        #endregion

        /////////////////////////////////////////////////////
        ///3.初始化
        #region
        public MainWindow()
        {
            InitializeComponent();

            ShowTaskInfoOnUI("正在初始化...");
            RegisterDefaultBooleanConverter();
            this.DataContext = taskProgress;
        }

        private void Windows_Initialized(object sender, EventArgs e)
        {
            //1.
            if (!CheckFFMpegFiles())
                return;
            //2.
            var width = myWindows.Width - 12 - 700 - 30;
            col5.Width = width - 10;
            gvcDownloadProgress.Width = width;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //1.
            InitView();
            //2.
            InitDownloadPath();
            //3.
            ParseMainJsonAsync();
        }

        private void RegisterDefaultBooleanConverter()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = new List<JsonConverter>
                {
                    new BooleanJsonConverter()
                }
            };
        }

        private void InitView()
        {
            myWindows.Title = "干净世界下载器" + getApplicationVersion();
            //1.
            LvDownloadItem.ItemsSource = downloadItemList;
            //2.
            threadNums.ForEach(num => cmbThreadNum.Items.Add(num));
            //3.
            btnTest.Visibility = test ? Visibility.Visible : Visibility.Collapsed;

        }

        private string getApplicationVersion()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Version? version = assembly.GetName().Version;
            if (version == null) return "";
            return $" V{version.Major:D}.{version.Minor:D}.{version.Build:D4}.{version.Revision:D4}";
        }

        private async void ParseMainJsonAsync()
        {
            try
            {
                Task<WebSite?> task = Task<WebSite>.Run(() =>
                {
                    return ParseMainJson();
                });
                webSite = await task;
                if (webSite == null)
                {
                    if (MessageBoxError("文件格式错误或者没有有效的设置信息！"))
                    {
                        Environment.Exit(0);
                    }
                    return;
                }
                Task<WebSiteDownloadHistory?> task2 = Task<WebSiteDownloadHistory>.Run(() =>
                {
                    return ParseDownloadHistoryJson();
                });
                webSiteDownloadHistory = await task2;
                InitWebSiteDownloadHistory();
                ShowTaskInfoOnUI("准备就绪，欢迎使用本程序！");
                //初始化线程数量
                InitThreadNumCombox(webSite.ThreadNum);
                ckbUseProxy.IsChecked = webSite.UseProxy;
                InitProxy(webSite);
                tbProxy.Text = $"{webSite.ProxyHost}:{webSite.ProxyPort}";
                //
                if (webSite.UserAgents != null && webSite.UserAgents.Count > 0)
                {
                    userAgent = webSite.UserAgents[new Random().Next(webSite.UserAgents.Count)];
                }
                Log($"App UserAgent = {userAgent}");
            }
            catch (Exception e)
            {
                Log(e.Message);
                if (MessageBoxError($"初始化网站及其模板出错！\n详情：{e.Message}"))
                {
                    Environment.Exit(0);
                }
            }
        }

        private WebSite? ParseMainJson()
        {
            try
            {
                if (!File.Exists(SETTINGS_JSON_FILE))
                    return null;
                var settings = new JsonSerializerSettings();
                //settings.Converters.Add(new StorageConverter());
                string jsonText = File.ReadAllText(SETTINGS_JSON_FILE);
                WebSite? webSite = JsonConvert.DeserializeObject<WebSite?>(jsonText);
                Log("ParseMainJson OK...");
                return webSite;
            }
            catch (Exception e)
            {
                Log("ParseMainJson ERROR: " + e.Message);
                return null;
            }
        }

        private WebSiteDownloadHistory? ParseDownloadHistoryJson()
        {
            try
            {
                if (!File.Exists(DOWNLOAD_HISTORY_JSON_FILE)) return null;

                var settings = new JsonSerializerSettings();
                //settings.Converters.Add(new StorageConverter());
                string jsonText = File.ReadAllText(DOWNLOAD_HISTORY_JSON_FILE);
                WebSiteDownloadHistory? downloadHistory = JsonConvert.DeserializeObject<WebSiteDownloadHistory>(jsonText);
                Log("ParseDownloadHistoryJson OK...");
                return downloadHistory;
            }
            catch (Exception e)
            {
                Log("ParseDownloadHistoryJson ERROR: " + e.Message);
                return null;
            }
        }

        private void InitDownloadPath()
        {
            try
            {
                if (!Directory.Exists(DOWNLOAD_PATH))
                {
                    Directory.CreateDirectory(DOWNLOAD_PATH);
                }
            }
            catch (Exception ex)
            {
                Log($"InitDownloadPath： 创建 {DOWNLOAD_PATH} 失败！详情：{ex.Message}");
            }
        }

        private void InitProxy(WebSite webSite)
        {
            //1.
            proxyState.UseProxy = webSite.UseProxy;
            proxyState.IsProxyChanged = false;
            proxyState.ProxyHost = webSite.ProxyHost;
            proxyState.ProxyPort = webSite.ProxyPort;
            //2.
            string errInfo = "";
            if (!IsProxyHostValid(webSite.ProxyHost))
            {
                errInfo += "主机";
            }

            if (!IsProxyPortValid(webSite.ProxyPort))
            {
                if (string.IsNullOrEmpty(errInfo))
                    errInfo += "端口";
                else
                    errInfo += "和端口";
            }
            if (string.IsNullOrEmpty(errInfo)) return;
            MessageBoxError("代理的" + errInfo + "设置错误！请修改。");
        }

        private bool CheckFFMpegFiles()
        {
            if (!File.Exists(FFMPEG_FILE))
            {
                MessageBoxError("没有找到ffmpeg.exe！\n程序需要此文件才能播放节目，请按照打开的说明文件下载！");
                System.Diagnostics.Process.Start("notepad.exe", README_FILE);
                Environment.Exit(0);
                return false;
            }
            return true;
        }

        #endregion

        /////////////////////////////////////////////////////
        ///4.退出数据保存
        #region
        private void Window_Closed(object sender, EventArgs e)
        {
            CheckProxyStateOnCloseApp();
            SaveMainJson();
            ClearTmpFile();
        }

        /// <summary>
        /// 代理的保存，为上一次正确运行的代理状态参数
        /// </summary>
        private void SaveMainJson()
        {
            //注意2个index要加 1
            try
            {
                //1.
                if (webSite == null) return;
                webSite.ThreadNum = threadNums[cmbThreadNum.SelectedIndex];
                webSite.UseProxy = (bool)(ckbUseProxy.IsChecked ?? false);
                string json = JsonConvert.SerializeObject(webSite, Formatting.Indented);
                File.WriteAllText(SETTINGS_JSON_FILE, json);
                //2.
                //2.1 检查是否需要保存
                //if (webSite == null) return;
                List<DownloadPackage> downloadPackages = PrepareDownloadPackageData();
                if (downloadPackages.Count == 0)
                {
                    File.WriteAllText(DOWNLOAD_HISTORY_JSON_FILE, "");
                    return;
                }
                //2.2 检查构造 webSiteHistory 和 currentSelection
                webSiteDownloadHistory = new WebSiteDownloadHistory(WEB_SITE_NAME, downloadPackages);
                //2.3
                webSiteDownloadHistory.CurrentTargetUrl = tbTaskTarget.Text;
                json = JsonConvert.SerializeObject(webSiteDownloadHistory, Formatting.Indented);
                File.WriteAllText(DOWNLOAD_HISTORY_JSON_FILE, json);
            }
            catch (Exception e1)
            {
                Log(e1.Message);
            }
        }

        private List<DownloadPackage> PrepareDownloadPackageData()//20240225 修改
        {
            var empty = new List<DownloadPackage>();
            if (webSite == null || downloadItemList.Count == 0) return empty;

            List<DownloadItem> savedItems;
            string mediaFilePath = GetMediaFileNameWithoutExtionForGjsj(downloadItemList[0].fileName);
            string mediaFileName = GetMediaFileNameForGjsj(downloadItemList[0].fileName);
            string filePath = $@"{DOWNLOAD_PATH}\{mediaFilePath}\{mediaFileName}";
            //最后合并的文件存在，且该文件大于一个设定的尺寸（暂时确定）表示最后合并成功，返回空表不再保存
            bool isAllSucc = downloadItemList.ToList().All(item => item.downloadResult);
            if (isAllSucc && File.Exists(filePath) && new FileInfo(filePath).Length > GJSJ_VALID_FILE_SIZE)//20240227 1MB
                return empty;
            savedItems = downloadItemList.ToList();
            return CollectDownloadPackage(savedItems);
        }

        private List<DownloadPackage> CollectDownloadPackage(List<DownloadItem> list)
        {
            var empty = new List<DownloadPackage>();
            try
            {
                if (list.Count == 0)
                {
                    Log("CollectDownloadPackage(): 没有需要保存的数据！");
                    return empty;
                }

                return list.ConvertAll(item =>
                {
                    var downloadPackage = new DownloadPackage();
                    downloadPackage.IsSaving = false;
                    downloadPackage.IsSaveComplete = item.downloadResult;
                    downloadPackage.SaveProgress = item.downloadProgress;
                    downloadPackage.Address = item.downloadUrl;
                    //long.TryParse(item.fileSize, out long totalFileSize);
                    downloadPackage.speedReceived = item.speedReceived;
                    downloadPackage.bytesReceived = item.bytesReceived;
                    downloadPackage.totalBytesToReceive = item.totalBytesToReceive;
                    downloadPackage.FileName = GetRelativePath(item.fullFileName);
                    return downloadPackage;
                });
            }
            catch (Exception ex)
            {
                Log($"CollectDownloadPackage() 出错：{ex.Message}");
                return empty;
            }
        }
        #endregion

        /////////////////////////////////////////////////////
        ///5.工具函数
        #region        
        private void InitWebSiteDownloadHistory()
        {
            if (webSiteDownloadHistory == null || webSiteDownloadHistory.DownloadPackages == null ||
                webSiteDownloadHistory.DownloadPackages.Count == 0)
                return;
            InitSavedDownloadPackages(webSiteDownloadHistory.DownloadPackages);
            tbTaskTarget.Text = webSiteDownloadHistory.CurrentTargetUrl;
        }

        private async Task<string> GetHtmlCodeAsync(string url)
        {
            return await FetchHtmlAsync(url);
        }

        private Task<string> FetchHtmlAsync(string url)
        {
            var t = Task.Run(() =>
            {
                return FetchHtml(url);
            });
            return t;
        }

        private string FetchHtml(string url)
        {
            try
            {
                //1.
                //2.
                HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(url);
                hwr.Headers.Add("User-Agent", userAgent);//20240912
                //设置下载请求超时为200秒
                hwr.Timeout = 15000;
                Log($"Proxy: useProxy = {proxyState.UseProxy}, host = {proxyState.ProxyHost}, port = {proxyState.ProxyPort}");
                if (proxyState.UseProxy)
                    hwr.Proxy = new WebProxy(proxyState.ProxyHost, proxyState.ProxyPort);
                //得到HttpWebResponse对象
                HttpWebResponse hwp = (HttpWebResponse)hwr.GetResponse();
                //根据HttpWebResponse对象的GetResponseStream()方法得到用于下载数据的网络流对象
                Stream ss = hwp.GetResponseStream();
                var ms = StreamToMemoryStream(ss);
                string html = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                //log("url ==> \n" + html);
                return html;
            }
            catch (Exception e)
            {
                Log(e.Message);
                return "";
            }
        }

        private MemoryStream StreamToMemoryStream(Stream inStream)
        {
            MemoryStream outStream = new MemoryStream();
            const int buffLen = 4096;
            byte[] buffer = new byte[buffLen];
            int count = 0;
            while ((count = inStream.Read(buffer, 0, buffLen)) > 0)
            {
                outStream.Write(buffer, 0, count);
            }
            return outStream;
        }

        private bool IsNumber(string text)
        {
            int number;

            //Allowing only numbers
            if (!(int.TryParse(text, out number)))
            {
                return false;
            }
            return true;
        }

        private string CalcMemoryMensurableUnit(double bytes)
        {
            double kb = bytes / 1024; // · 1024 Bytes = 1 Kilobyte 
            double mb = kb / 1024; // · 1024 Kilobytes = 1 Megabyte 
            double gb = mb / 1024; // · 1024 Megabytes = 1 Gigabyte 
            double tb = gb / 1024; // · 1024 Gigabytes = 1 Terabyte 

            string result =
                tb > 1 ? $"{tb:0.0}TB" : //0.##
                gb > 1 ? $"{gb:0.0}GB" : //0.##
                mb > 1 ? $"{mb:0.0}MB" : //0.##
                kb > 1 ? $"{kb:0.0}KB" : //0.##
                $"{bytes:0.0}B";

            result = result.Replace("/", ".");
            return result;
        }

        public void Log(string msg)
        {
            //Console.WriteLine(msg);
            Debug.WriteLine(msg);
        }

        private string PatchTitle(string title)
        {
            title = title.Trim();
            int pos = title.IndexOf('|');
            if (pos > 0)
                title = title.Substring(0, pos);
            //20220609 剔除里面看不见的不合规则的字符（实际却是不可见！！！），但是\r\n单独处理
            title = Regex.Replace(title, @"[\\/:*?<>|]", " ");//20250815 多个.导致文件名以至于下载出现错误
            title = title.Replace("｜", " ").Replace("\n", " ").Replace("\r", " ").Replace("\"", " ").Replace("\r\n", " ");
            //20240304 处理连续多个无意义空格的问题；20250815 多个.导致文件名以至于下载出现错误
            return Regex.Replace(title, @"[. ]{2,}", " ").Trim();
        }

        private string ComputeMD5(string source)
        {
            using (var md5 = MD5.Create())
            {
                var data = md5.ComputeHash(Encoding.UTF8.GetBytes(source));
                StringBuilder builder = new StringBuilder();
                // 循环遍历哈希数据的每一个字节并格式化为十六进制字符串 
                for (int i = 0; i < data.Length; i++)
                {
                    builder.Append(data[i].ToString("X2"));
                }
                string result = builder.ToString().Substring(8, 16);
                Log("方式4：" + result);
                return result;
            }
        }

        private string GetMediaPartFileNameWithoutExtionForGjsj(string fileName)
        {
            return Path.GetFileNameWithoutExtension(fileName);
        }

        private string GetMediaFileNameWithoutExtionForGjsj(string fileName)
        {
            return Path.GetFileNameWithoutExtension(Regex.Replace(fileName, $"^({GJSJ_MEDIA_PART_VIDEO}|{GJSJ_MEDIA_PART_AUDIO})", ""));
        }

        private string GetMediaFileNameForGjsj(string fileName)
        {
            return Path.GetFileName(Regex.Replace(fileName, $"^({GJSJ_MEDIA_PART_VIDEO}|{GJSJ_MEDIA_PART_AUDIO})", ""));
        }

        private string GetRelativePath(string fullFileName)
        {
            return fullFileName.Replace(DOWNLOAD_PATH, "");
        }

        private void ClearTmpFile()
        {
            if (File.Exists(TMP_FILE))
                File.Delete(TMP_FILE);
        }


        #endregion

        /////////////////////////////////////////////////////
        ///6.获取下载链接
        #region 
        //link: 如果 ParseByUrl 为 T，则为网页链接url; 否则，为文件路径path。
        private void FecthDownloadUrls()
        {
            //1.检查网站的支持情况
            //2.检查网络状态
            if (!ValidateProxy()) return;
            //3.检查要解析的连接或文件
            string url = tbTaskTarget.Text.Trim();
            if (!CheckTbWebUrl(url)) return;
            //4.检查当前完成状态
            var list = CheckDownloadUrlState(url);
            //4.1 [1]获取完成，不需要从新获取
            if (list == null) return;
            CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, true);
            //4.2 [2]继续完成未完成的(TS的不存在这种情况，属于一次获取，询问质量，一次下载和解析完成)
            //if (list.Count > 0)
            //{
            //    FetchUnfinishedDownloadUlrs(list);
            //    return;
            //}
            //4.3 [3]重新下载
            //获取或从新下载（更新），主要需要剔除【 有标题 而 无中间连接】 的无效item ，
            //不过一般网页不会出错，不会出现 无效item。
            downloadItemList.Clear();
            FetchDownloadUrlForGJSJ(url);
        }

        private bool CheckTbWebUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                MessageBoxError("“网页链接”为空！");
                return false;
            }
            if (!url.StartsWith("http", false, null))
            {
                MessageBoxError("“网页链接”格式错误！");
                return false;
            }
            return true;
        }

        private bool IsAllFilesDownloaded()
        {
            if (downloadItemList.Count > 0 && downloadItemList.All(item => item.downloadResult == true))
                return true;
            return false;
        }

        /// <summary>
        /// 检查是否需要重新下载，或只是更新未获取下载链接的项（强制把剩余的获取完成，不考虑 选择性问题）
        /// 1.保持现状(有，且已经完成)，返回 null
        /// 2.要清空，这返回空列表
        /// 3.要继续未完成的，返回有效列表
        /// 【注意】选择和不选择，都必须把 downloadUrl 全部获取才能進入到下一步
        /// </summary>
        /// <returns></returns>
        private List<DownloadItem>? CheckDownloadUrlState(string url)
        {
            List<DownloadItem> items = new List<DownloadItem>();
            //1.20220619 检查是否是新的，新的连接或文件，则清空旧数据，就从新下载
            var isNewWebUrl = !string.IsNullOrEmpty(oldTaskTarget) && oldTaskTarget != url;
            if (isNewWebUrl)
            {
                oldTaskTarget = url;
                downloadItemList.Clear();
                return items;
            }
            //2.检查列表为空的状态
            if (downloadItemList.Count == 0)
                return items;
            //3.当前的连接或文件没有变化，检查其状态，决定下载情况
            bool isAllFetced = IsAllFilesDownloaded();//20240222 IsAllDownloadUrlFetched();
            if (isAllFetced)
            {
                if (MessageBoxQuestion("下载列表不为空，且已经成功获取下载链接。如果继续则会清空列表，从新获取下载列表。继续请按“确定”按钮"))
                {
                    return items;
                }
                else
                {
                    return null;
                }
            }
            //3.【20220612】 中间连接需要考虑，但是只要剔除了【 有标题 而 无中间连接 ==> 这样的情况几乎不存在】 的Item
            var unfinishedDownloadLinks = downloadItemList.Where(item => string.IsNullOrEmpty(item.downloadUrl)).ToList();
            if (unfinishedDownloadLinks.Any(item => string.IsNullOrEmpty(item.intermediateUrl)))
            {
                return items;//其中任一一项存在 intermediateUrl 无效的情况，就得重新获取
            }
            else
            {
                return unfinishedDownloadLinks;
            }
        }

        private string PatchFileExtension(string fileName)
        {
            if (webSite == null)
            {
                MessageBoxError("[E0]: 参数出错！");
                return fileName;
            }
            if (fileName.ToLower().EndsWith(webSite.FileExtension.ToLower())) return fileName;
            return fileName + webSite.FileExtension;
        }

        private bool ValidateProxy()
        {
            bool useProxy = (bool)(ckbUseProxy.IsChecked ?? false);
            if (webSite != null)
                webSite.UseProxy = useProxy;
            proxyState.UseProxy = useProxy;
            if (!useProxy) return true;

            return ValidateTextBoxProxy();
        }

        private (string host, string port) ParseTbProxy()
        {
            string proxy = tbProxy.Text.Trim();
            var para = proxy.Split(':');
            if (para.Length != 2)
            {
                return ("", "");
            }
            return (para[0], para[1]);
        }

        private bool ValidateTextBoxProxy()
        {
            if (webSite == null) return false;

            var (sHost, sPort) = ParseTbProxy();
            if (!IsProxyHostValid(sHost))
            {
                MessageBoxError("代理的主机设置错误！");
                return false;
            }
            if (!IsProxyPortValid(sPort))
            {
                MessageBoxError("代理的端口设置错误！");
                return false;
            }

            int proxyPort = Int32.Parse(sPort);

            proxyState.IsProxyChanged = webSite.ProxyHost != sHost || webSite.ProxyPort != proxyPort;
            proxyState.ProxyHost = sHost;
            proxyState.ProxyPort = proxyPort;
            webSite.ProxyHost = sHost;
            webSite.ProxyPort = proxyPort;
            if (proxyState.IsProxyChanged)
            {
                SaveMainJson();
            }
            Log($"ValidateTextBoxProxy: useProxy = {proxyState.UseProxy}, isProxyChanged = {proxyState.IsProxyChanged}, host = {proxyState.ProxyHost}, port = {proxyState.ProxyPort}");
            return true;
        }

        private void CheckProxyStateOnCloseApp()
        {
            if (webSite == null) return;
            webSite.UseProxy = (bool)(ckbUseProxy.IsChecked ?? false);

            var (sHost, sPort) = ParseTbProxy();
            if (!IsProxyHostValid(sHost) || !IsProxyPortValid(sPort))
            {
                MessageBoxError("代理设置错误！不会保存最新的代理设置。");
                return;
            }
            int proxyPort = Int32.Parse(sPort);
            webSite.ProxyHost = sHost;
            webSite.ProxyPort = proxyPort;
        }

        private bool IsProxyHostValid(string host)
        {
            return !string.IsNullOrEmpty(host) && Regex.IsMatch(host, @"^([\w-]+\.)+[\w-]+(/[\w-./?%&=]*)?$");
        }

        private bool IsProxyPortValid(string portString)
        {
            if (string.IsNullOrEmpty(portString)) return false;
            if (!Regex.IsMatch(portString, @"\d+")) return false;
            int port = Int32.Parse(portString);
            return port >= 1024 && port <= 65535;
        }

        private bool IsProxyPortValid(int port)
        {
            return port >= 1024 && port <= 65535;
        }

        #endregion

        /////////////////////////////////////////////////////
        ///7.下载 20220605
        #region   
        private async void DownloadFromGJSJ(CancellationToken cancellationToken = default)
        {
            //0.
            errors.Clear();
            //1.检查网络状态
            if (!ValidateProxy()) return;
            //2.检查当前完成状态
            if (downloadItemList.Count == 0)
            {
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, "下载列表为空！");
                return;
            }
            if (downloadItemList.Count > 2)
            {
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, "当前视频下载项目出现异常，超过2个下载子项！");
                return;
            }
            //3.
            VideoDL videoDL = proxyState.UseProxy ? new VideoDL(proxy: $"http://{proxyState.ProxyHost}:{proxyState.ProxyPort}") : new VideoDL();//proxy: "socks5://127.0.0.1:8000"
            var hlsDL = videoDL.Hls;
            string mediaFilePath = GetMediaFileNameWithoutExtionForGjsj(downloadItemList[0].fileName);//???
            string parentDir;// = DOWNLOAD_PATH + $@"\{mediaFilePath}";//???
            bool isSucc;

            downloadList.Clear();
            downloadList.AddRange(downloadItemList);
            int taskProgressStartPoint = 0;
            //3.1 旧模式
            if (!IsNewGjsjHslMode(downloadItemList[0].fileName))
            {
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, true);
                parentDir = DOWNLOAD_PATH;//【1】
                if (IsGjsjMediaDownloaded(downloadItemList[0]))
                {
                    if (MessageBoxQuestion("已经成功下载视频，如果继续则会从新下载。继续请按“确定”按钮"))
                    {
                        downloadItemList[0].downloadResult = false;
                    }
                    else
                    {
                        return;
                    }
                }
                isSucc = await DownloadOneMediaPartForGjsj(downloadItemList[0], hlsDL, parentDir, mediaPartNum: 1, cancellationToken: cancellationToken);
                if (isSucc)
                {
                    taskProgress.TaskProgress = 100;
                    CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false);
                }
                return; //完成后返回。
            }
            //3.2 新模式 子项目一定是2个，每个都存在下载成功与否的状态
            if (downloadItemList.Count == 1)
            {
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, "当前节目下载项目出现异常，应有视频和音频共2个下载子项！");
                return;
            }
            //3.2.1 调整下载项目与提示 ==> 20260227 取消此部分，没有太大用，使用ToolTip提示。
            /*bool isPart1Downloaded = IsGjsjMediaPartDownloaded(downloadItemList[0]);
            bool isPart2Downloaded = IsGjsjMediaPartDownloaded(downloadItemList[1]);
            Enum newDownloadStateOfGjsj = NewDownloadStateOfGjsj.STATE_NONE;
            string tip = "";
            if (isPart1Downloaded && isPart2Downloaded)
            {
                tip += "已经成功下载节目的视频和音频部分";
                newDownloadStateOfGjsj = NewDownloadStateOfGjsj.STATE_VIDEO_AUDIO_OK;
            }
            else if (isPart1Downloaded && !isPart2Downloaded)
            {
                if (IsNewGjsjMediaPartVideo(downloadItemList[0].fileName))
                {
                    tip += "已经成功下载节目的视频部分";
                    newDownloadStateOfGjsj = NewDownloadStateOfGjsj.STATE_VIDEO_OK;
                }
                else
                {
                    tip += "已经成功下载节目的音频部分";
                    newDownloadStateOfGjsj = NewDownloadStateOfGjsj.STATE_AUDIO_OK;
                }
            }
            else if (!isPart1Downloaded && isPart2Downloaded)
            {
                if (IsNewGjsjMediaPartVideo(downloadItemList[1].fileName))
                {
                    tip += "已经成功下载节目的视频部分";
                    newDownloadStateOfGjsj = NewDownloadStateOfGjsj.STATE_VIDEO_OK;
                }
                else
                {
                    tip += "已经成功下载节目的音频部分";
                    newDownloadStateOfGjsj = NewDownloadStateOfGjsj.STATE_AUDIO_OK;
                }
            }
            if (!string.IsNullOrEmpty(tip))
            {                
                if (MessageBoxQuestion(tip + "，若选择“确定”则会从新下载已下载项目，选择“取消”则不会从新下载。下载完成或处于下载完成状态的音视频部分会在最后阶段直接进入合并视频的流程。"))
                {
                    switch (newDownloadStateOfGjsj)
                    {
                        case NewDownloadStateOfGjsj.STATE_VIDEO_AUDIO_OK:
                            downloadItemList[0].downloadResult = false;
                            downloadItemList[1].downloadResult = false;
                            break;
                        case NewDownloadStateOfGjsj.STATE_VIDEO_OK:
                            downloadItemList.Where(item => IsNewGjsjMediaPartVideo(item.fileName)).ElementAt(0).downloadResult = false;
                            break;
                        case NewDownloadStateOfGjsj.STATE_AUDIO_OK:
                            downloadItemList.Where(item => !IsNewGjsjMediaPartVideo(item.fileName)).ElementAt(0).downloadResult = false;
                            break;
                        case NewDownloadStateOfGjsj.STATE_NONE:
                            break;
                    }
                }
            }*/
            //3.2.2
            CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, true);
            parentDir = DOWNLOAD_PATH + $@"\{mediaFilePath}";//【2】
            for (int i = 0; i < downloadItemList.Count; i++)
            {
                if (downloadItemList[i].downloadResult)
                {
                    taskProgressStartPoint += 45;
                    taskProgress.TaskProgress = taskProgressStartPoint;
                    continue;
                }

                isSucc = await DownloadOneMediaPartForGjsj(downloadItemList[i], hlsDL, parentDir, taskProgressStartPoint: taskProgressStartPoint, cancellationToken: cancellationToken);
                if (isSucc)
                {
                    taskProgressStartPoint += 45;
                    taskProgress.TaskProgress = taskProgressStartPoint;
                }
                else
                {
                    return;
                }
            }
            //3.2.3 直接用 mediaFilePath 或者网页 title 解析的结果——无扩展名的名字，因为 Muxing 后会自动添加 .mp4
            if (await MuxingMediaPartForGjsj(hlsDL, parentDir, mediaFilePath/* + ".mp4"*/))
            {
                string dir;
                for (int i = 0; i < downloadItemList.Count; i++)
                {
                    dir = parentDir + "\\" + Path.GetFileNameWithoutExtension(downloadItemList[i].fileName);
                    Log("清理文件夹 ==> " + dir);
                    Directory.Delete(dir, true);
                }
            }
            CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false);//20240717
        }

        private async Task<bool> DownloadOneMediaPartForGjsj(DownloadItem item, VideoDL_m3u8.DL.HlsDL hlsDL, string parentDir, int mediaPartNum = 2, int taskProgressStartPoint = 0, CancellationToken cancellationToken = default)
        {
            try
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.fileName);//【非常重要】下载部分会自动添加扩展名，此处只需提供无扩展名的文件名，避免形成 .mp4.mp4 的错误
                //return await DownloadGJSJMediaPart(hlsDL, parentDir, item.id, item.downloadUrl, item.fileName, mediaPartNum: mediaPartNum, taskProgressStartPoint: taskProgressStartPoint);
                return await DownloadGJSJMediaPart(hlsDL, parentDir, item.id, item.downloadUrl, fileNameWithoutExtension, header: $"User-Agent:{userAgent}", mediaPartNum: mediaPartNum, taskProgressStartPoint: taskProgressStartPoint, cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                Log("Error: " + e.Message);
                if (e is OperationCanceledException) return false;
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, e.Message);
                return false;
            }
        }

        private async Task<bool> MuxingMediaPartForGjsj(VideoDL_m3u8.DL.HlsDL hlsDL, string parentDir, string lastFileName)
        {
            ShowTaskInfoOnUI("正在合并视频");
            try
            {
                if (!await MuxingAysnc(hlsDL, parentDir, lastFileName, downloadItemList[0].fullFileName, downloadItemList[1].fullFileName))
                {
                    CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, $"合并 {lastFileName} 失败！");
                    return false;
                }
                taskProgress.TaskProgress = 100;
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false);
                return true;
            }
            catch (Exception e)
            {
                Log("Error: " + e.Message);
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, e.Message);
                return false;
            }
        }

        private void UpdateDwonloadInfoForGJSJ(int taskId, VideoDL_m3u8.Events.ProgressEventArgs args, int mediaPartNum, int taskProgressStartPoint)
        {
            DownloadItem item = downloadList[taskId];//???

            item.speedReceived = args.Speed;
            item.bytesReceived = args.DownloadBytes;
            item.totalBytesToReceive = args.TotalBytes;
            //string avgSpeed = CalcMemoryMensurableUnit(e.AverageBytesPerSecondSpeed);
            string speed = CalcMemoryMensurableUnit(args.Speed);// e.BytesPerSecondSpeed);
            string bytesReceived = CalcMemoryMensurableUnit(args.DownloadBytes); //.ReceivedBytesSize);
            string totalBytesToReceive = CalcMemoryMensurableUnit(args.TotalBytes);//.TotalBytesToReceive);
            //string progressPercentage = $"{args.Percentage:F3}".Replace("/", ".");
            double percent = args.Percentage * 100;
            Log($"TaskID:{taskId} => ProgressPercentage={percent}, totalBytesToReceive={totalBytesToReceive}");
            if (percent >= 100)
            {
                item.downloadResult = true;
                Log($"OnDownloadProgressChanged [{taskId}] 下载完成");
            }
            taskProgress.TaskProgress = taskProgressStartPoint + 20 + percent * (60 / mediaPartNum) / 100;//20240223 整个下载占总进程的60%： 20-80%之间
            item.downloadProgress = (int)(percent);//ProgressPercentage;
            item.downloadSpeed = $"{speed}/s";// $"{speed}/s | ---/s";
            item.fileSize = $"{bytesReceived} / {totalBytesToReceive}";
        }

        //20231123 专用于干净世界下载，一次下载一个
        private async Task<bool> DownloadGJSJMediaPart(VideoDL_m3u8.DL.HlsDL hlsDL, string parentDir, int taskId, string url, string saveName, string header = "",/*int? maxHeight = null, */ int mediaPartNum = 2, int taskProgressStartPoint = 0, CancellationToken cancellationToken = default)
        {
            try
            {
                string taskInfo = GetTaskInfoForGJSJ(saveName);
                ShowTaskInfoOnUI(taskInfo);
                taskProgress.TaskProgress = taskProgressStartPoint;
                //1、准备
                //1.1 为每一个下载设定一个目录，原因有二，一是干净世界都是同名的segment.ts的下载片段；二是若没下载完，切换到别的下载后，再次切换回来还可以接续下载
                //saveName = PathExtension.FilterFileName(saveName);
                var workDir = parentDir + $"\\{saveName}";
                if (!Directory.Exists(workDir))//20240228 必须自己创建（只能一次一个），否则会出现错误属性的文件夹，不能删也不能改
                {
                    Directory.CreateDirectory(workDir);
                }
                //1.2、准备m3u8下载等
                //2、下载m3u8文件。因为程序已经提供了用户所选择的特定分辨率的m3u8，因此不需要再下载 mater.m3u8 以及后续的判断与处理了
                var (manifest, m3u8Url) = await hlsDL.GetManifestAsync(url, header, token: cancellationToken);
                taskProgress.TaskProgress = taskProgressStartPoint + 5 / mediaPartNum;

                //2.1 [忽略]检查 master.m3u8 
                /*if (HlsExtension.IsMaster(manifest))
                {
                    // Parse m3u8 manifest to master playlist
                    var masterPlaylist = hlsDL.ParseMasterPlaylist(manifest, m3u8Url);

                    // Choose the highest quality resolution
                    var highestStreamInfo = masterPlaylist.StreamInfos.GetWithHighestQuality(maxHeight);
                    if (highestStreamInfo == null)
                        throw new Exception("Not found stream info.");
                    (manifest, m3u8Url) = await hlsDL.GetManifestAsync(highestStreamInfo.Uri, header, token: cancellationToken);
                }*/
                //2.2 下载 直播的 index.m3u8，并解析下载的媒体列表
                var mediaPlaylist = hlsDL.ParseMediaPlaylist(manifest, m3u8Url);
                taskProgress.TaskProgress = taskProgressStartPoint + 10 / mediaPartNum;
                //2.3 下载 m3u8 segment key
                var keys = null as Dictionary<string, string>;
                var segmentKeys = hlsDL.GetKeys(mediaPlaylist.Parts);
                if (segmentKeys.Count > 0)
                    keys = await hlsDL.GetKeysDataAsync(segmentKeys, header, token: cancellationToken);
                taskProgress.TaskProgress = taskProgressStartPoint + 15 / mediaPartNum;

                //3、下载第一段视频
                //3.1 下载
                var firstSegment = await hlsDL.GetFirstSegmentAsync(
                    workDir, saveName, mediaPlaylist.Parts, header, keys,
                    onSegment: async (ms, token) =>
                    {
                        // Detect and skip png header
                        return await ms.TrySkipPngHeaderAsync(token);
                    },
                    token: cancellationToken);
                //3.2 [忽略]解析
                Log("加载视频信息...");
                var videoInfos = await VideoDL_m3u8.Utils.FFmpeg.GetVideoInfo(firstSegment, token: cancellationToken);
                videoInfos.ForEach(it => Log(it));
                taskProgress.TaskProgress = taskProgressStartPoint + 20 / mediaPartNum;

                //4、下载整个视频ts文件
                Log("下载中...");
                await hlsDL.DownloadAsync(workDir, saveName,
                    mediaPlaylist.Parts, header, keys,
                    threads: (webSite?.ThreadNum ?? 3), maxRetry: 6,
                    onSegment: async (ms, token) =>
                    {
                        // 检测和跳过 png header
                        return await ms.TrySkipPngHeaderAsync(token);
                    },
                    progress: (args) =>
                    {
                        var print = args.Format;
                        Log(print);
                        //var sub = Console.WindowWidth - 2 - print.Length;
                        //Console.Write("\r" + print + new string(' ', sub) + "\r");
                        UpdateDwonloadInfoForGJSJ(taskId, args, mediaPartNum, taskProgressStartPoint);
                    },
                    token: cancellationToken);
                taskProgress.TaskProgress = taskProgressStartPoint + 80 / mediaPartNum;

                //5、合并ts文件，并用 FFmpeg 转换为mp4
                Log("合并文件...");
                await hlsDL.MergeAsync(workDir, saveName,
                    clearTempFile: true,
                    onMessage: (msg) =>
                    {
                        //Console.ForegroundColor = ConsoleColor.DarkYellow;
                        //Console.Write(msg);
                        //Console.ResetColor();
                        MessageBoxErrorWithoutResultOnUI(msg);
                    },
                    token: cancellationToken);
                taskProgress.TaskProgress = taskProgressStartPoint + (mediaPartNum == 1 ? 100 : 90) / mediaPartNum;
                //CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false); //20240717 取消
                return true;
            }
            catch
            {
                throw;
            }
        }

        private async Task<bool> MuxingAysnc(VideoDL_m3u8.DL.HlsDL hlsDL, string parentDir, string saveName, string videoPath, string audioPath, CancellationToken cancellationToken = default)
        {
            try
            {
                //saveName = PathExtension.FilterFileName(saveName);
                //var workDir = parentDir + $"\\{saveName}";
                await hlsDL.MuxingAsync(
                                        parentDir, saveName, videoPath, audioPath,
                                        onMessage: (msg) =>
                                        {
                                            CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, msg);
                                        },
                                        token: cancellationToken);
                taskProgress.TaskProgress = 100;
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false);
                return true;
            }
            catch (Exception e)
            {
                CtrolWidgetsOnTask(AppTask.TASK_DOWNLOAD, false, e.Message);
                return false;
            }
        }

        private string GetTaskInfoForGJSJ(string saveName)
        {
            string taskInfo;
            if (saveName.Contains(GJSJ_MEDIA_PART_VIDEO))
            {
                taskInfo = "正在下载视频部分";
            }
            else if (saveName.Contains(GJSJ_MEDIA_PART_AUDIO))
            {
                taskInfo = "正在下载音频部分";
            }
            else
            {
                taskInfo = "正在下载视频";
            }
            return taskInfo;
        }

        private bool IsGjsjMediaDownloaded(DownloadItem item)
        {
            if (!item.downloadResult) return false;
            string mediaFileName = GetMediaFileNameWithoutExtionForGjsj(item.fileName);
            string lastFile = DOWNLOAD_PATH + $@"\{mediaFileName}\{item.fileName}";//.mp4
            return File.Exists(lastFile) && new FileInfo(lastFile).Length > GJSJ_VALID_FILE_SIZE; //
        }

        private bool IsGjsjMediaPartDownloaded(DownloadItem item)
        {
            if (!item.downloadResult) return false;
            return File.Exists(item.fullFileName) && new FileInfo(item.fullFileName).Length > GJSJ_VALID_FILE_SIZE; //1M
        }

        private bool IsNewGjsjHslMode(string fileName)
        {
            return fileName.Contains(GJSJ_MEDIA_PART_VIDEO) || fileName.Contains(GJSJ_MEDIA_PART_AUDIO);
        }

        private bool IsNewGjsjMediaPartVideo(string fileName)
        {
            return fileName.Contains(GJSJ_MEDIA_PART_VIDEO);
        }

        #endregion

        /////////////////////////////////////////////////////
        ///8.控件控制
        #region
        private void InitThreadNumCombox(int threadNum)
        {
            int pos = threadNums.IndexOf(threadNum);
            int posDefault = threadNums.IndexOf(DEFAULT_THREAD_NUM);
            cmbThreadNum.SelectedIndex = pos == -1 ? posDefault : pos;
        }

        private void InitSavedDownloadPackages(List<DownloadPackage> packages)
        {
            try
            {
                downloadItemList.Clear();
                if (packages == null || packages.Count == 0)
                {
                    Log("没有保存的下载信息！");
                    return;
                }
                int index = 0;
                string fileName, folderPath, directoryName;
                packages.ForEach(package =>
                {
                    fileName = Path.GetFileName(package.FileName);
                    directoryName = (Path.GetDirectoryName(package.FileName) ?? "").TrimEnd('\\');
                    folderPath = DOWNLOAD_PATH + (string.IsNullOrEmpty(directoryName) ? "" : $@"{directoryName}");
                    DownloadItem item = new DownloadItem(index, fileName, "", package.Address, folderPath: folderPath, downloadResult: package.IsSaveComplete);
                    string downloadSpeed = CalcMemoryMensurableUnit(package.speedReceived); //20240508
                    string bytesReceived = CalcMemoryMensurableUnit(package.bytesReceived);
                    string totalBytesToReceive = CalcMemoryMensurableUnit(package.totalBytesToReceive);
                    var fileSize = $"{bytesReceived}/{totalBytesToReceive}";//20240227 ???

                    item.setDownloadInfo((int)package.SaveProgress, downloadSpeed, fileSize, package.IsSaveComplete);
                    Log($"InitSavedDownloadPackages: {index}-{item.fileName}");
                    downloadItemList.Add(item);

                    index++;
                });

            }
            catch (Exception ex)
            {
                Log($"InitSavedDownloadPackages 出现错误：{ex.Message}");
            }
        }

        #endregion

        /////////////////////////////////////////////////////
        ///9. 控件控制与消息显示
        #region
        /// <summary>
        /// startTask=false, msg 不空时，表示有错误发生。
        /// </summary>
        /// <param name="task"></param>
        /// <param name="startTask"></param>
        /// <param name="msg"></param>
        private void CtrolWidgetsOnTask(AppTask task, bool startTask, string errDetail = "")
        {
            string taskInfo = "";
            string errInfo = "";
            bool noErrInfo = string.IsNullOrEmpty(errDetail);
            switch (task)
            {
                case AppTask.TASK_FETCH_DOWNLOAD_URL:
                    abFetchDownloadUrl.Set(startTask);
                    taskInfo = startTask ? "正在获取下载链接" : (noErrInfo ? "完成获取下载链接任务！" : "获取下载链接出错");
                    if (!noErrInfo) errInfo = $"获取下载链接出错！详情：{errDetail}";
                    break;
                case AppTask.TASK_DOWNLOAD:
                    abStartDownload.Set(startTask);
                    taskInfo = startTask ? "正在下载文件" : (noErrInfo ? "完成下载任务！" : "下载出错");
                    if (!noErrInfo) errInfo = $"下载出错！详情：{errDetail}";
                    break;
                case AppTask.TASK_MERGE_TS_FILES:
                    abMergeTSFiles.Set(startTask);
                    taskInfo = startTask ? "正在合并TS文件" : (noErrInfo ? "完成合并TS文件任务！" : "合并TS文件出错");
                    if (!noErrInfo) errInfo = $"合并TS文件出错！详情：{errDetail}";
                    break;
            }
            ShowTaskInfoOnUI(taskInfo);
            if (!noErrInfo) MessageBoxErrorWithoutResultOnUI(errInfo);
            EnableWidgesOnUI(task, !startTask);
            if (startTask)
                ResetProgressBar();
        }

        private void ShowTaskInfoOnUI(string msg)
        {
            this.Dispatcher.Invoke(() =>
            {
                lbTaskInfo.Content = msg;
            });
        }

        private void ResetProgressBar()
        {
            pbTaskInfo.Value = 0;
        }

        private void EnableWidgesOnUI(AppTask task, bool isEnabled)
        {
            this.Dispatcher.Invoke(() =>
            {
                tbTaskTarget.IsEnabled = isEnabled;

                btnFetchDownloadLinks.IsEnabled = isEnabled;
                btnStartDownload.IsEnabled = isEnabled;
                btnStopDownload.IsEnabled = task == AppTask.TASK_DOWNLOAD ? true : isEnabled;

                ckbUseProxy.IsEnabled = isEnabled;
                tbProxy.IsEnabled = isEnabled;
                cmbThreadNum.IsEnabled = isEnabled;
            });
        }

        private void MessageBoxInformationWithoutResultOnUI(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(this, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            });

        }

        private void MessageBoxErrorWithoutResultOnUI(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(this, message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private bool MessageBoxInformation(string message)
        {
            return MessageBox.Show(this, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information) == MessageBoxResult.OK;
        }

        private bool MessageBoxError(string message)
        {
            return MessageBox.Show(this, message, "错误", MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK;
        }

        private bool MessageBoxQuestion(string message)
        {
            return MessageBox.Show(this, message, "询问", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        private ProgressDialog ShowProgressDialog(string message, string title = "提示")
        {
            ProgressDialog dialog = new ProgressDialog();
            dialog.Owner = this;
            dialog.Title = title;
            dialog.lbTaskInfo.Content = message;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Show();
            return dialog;
        }

        #endregion

        /////////////////////////////////////////////////////
        ///10. 一般控件事件【1】
        #region 
        private void tbDownloadUrl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                //MessageBoxInformation(((TextBlock)sender).Text);
                string m3u8Url = ((TextBlock)sender).Text;//20231123 直接赋值为index.m3u8了。 ==>.Replace("segment.ts", "index.m3u8");
                Clipboard.SetDataObject(m3u8Url);
                MessageBoxTimeoutA((IntPtr)0, "已将下载连接复制到剪贴板上了！", "提示", 0, 0, 3000);
                e.Handled = true;
            }
        }

        private void cmbThreadNum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (webSite == null) return;
            webSite.ThreadNum = threadNums[cmbThreadNum.SelectedIndex];
        }

        private void btnLvItemPause_Click(object sender, RoutedEventArgs e)
        {
            int index = (int)((Button)sender).Tag;
            Log($"btnLvItemPause_Click: index={index}");
            //downloadItemList[index].isSelected = select;
        }

        private void ckbUseProxy_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = ckbUseProxy.IsChecked ?? false;
            bdProxySelection.BorderBrush = isChecked ? null : Brushes.Red;
        }
        #endregion

        /////////////////////////////////////////////////////
        ///11. 任务控件事件【2】
        #region
        private void btnFetchDownloadLinks_Click(object sender, RoutedEventArgs e)
        {
            if (downloadItemList.Count > 0)
            {
                if (MessageBoxQuestion("下载列表不为空且有尚未完成的下载任务，若继续，则会取消未完成的下载任务并清空下载列表。继续，请按确认按钮；取消，请按取消按钮。"))
                {
                    downloadItemList.Clear();
                }
                else
                {
                    return;
                }
            }
            FecthDownloadUrls();
        }

        private void btnStartDownload_Click(object sender, RoutedEventArgs e)
        {
            if (webSite == null)
            {
                MessageBoxError("[E0]: 参数出错！");
                return;
            }
            cancellationToken = new CancellationTokenSource();
            DownloadFromGJSJ(cancellationToken.Token);
        }

        private void btnStopDownload_Click(object sender, RoutedEventArgs e)
        {
            if (webSite == null)
            {
                MessageBoxError("[E0]: 参数出错！");
                return;
            }

            abStartDownload.Set(false);
            ShowTaskInfoOnUI("下载任务被取消");
            EnableWidgesOnUI(AppTask.TASK_DOWNLOAD, true);
            cancellationToken?.Cancel(true);

        }
        #endregion

        /////////////////////////////////////////////////////
        ///12. 干净世界视频下载 
        #region
        //const bool GJSJ_DOWNLOAD_BY_BYTE_RANGE = false;//作为单一ts文件下载
        //const bool ENABLE_VIDEO_CONVERTOR_BUTTON = false;//
        private async void FetchDownloadUrlForGJSJ(string url)
        {
            //string url = "";
            //url = "";

            /*string url = tbTaskTarget.Text;
            if(url.Length == 0)
            {
                MessageBoxInformation("干净世界视频网页连接为空，请输入！");
                return;
            }*/
            string videoName = await ParseGjsjVideoUrlAsyn(url);
            if (string.IsNullOrEmpty(videoName))
            {
                //MessageBoxInformation("干净世界视频网页连接为空，请输入！");
                return;
            }

            List<DownloadItem> downloadItems = new List<DownloadItem>();
            int videoIndex, audioIndex;
            //videoName = PatchFileExtension(videoName); //20240228 取消 出现重复，下载部分会内在的加上合适的扩展名
            string fileExtention = webSite?.FileExtension ?? "";
            string videoFileFolderPath = $@"{DOWNLOAD_PATH}\{videoName}\{GJSJ_MEDIA_PART_VIDEO + videoName}";
            string audioFileFolderPath = $@"{DOWNLOAD_PATH}\{videoName}\{GJSJ_MEDIA_PART_AUDIO + videoName}";
            if (GjsjAudioQualityList.Count > 0)
            {
                (videoIndex, audioIndex) = showVideoAudioQualitySelectionDialog(GjsjVideoQualityList, GjsjAudioQualityList);
                downloadItems.Add(new DownloadItem(0, GJSJ_MEDIA_PART_VIDEO + videoName + fileExtention, "", MasterPlaylist.StreamInfos[videoIndex].Uri, folderPath: videoFileFolderPath));//20240226
                downloadItems.Add(new DownloadItem(1, GJSJ_MEDIA_PART_AUDIO + videoName + fileExtention, "", MasterPlaylist.MediaGroups[audioIndex].Uri, folderPath: audioFileFolderPath));//20240226
                btnStartDownload.ToolTip = "【提示】如节目音、视频两子项均已下载完成，但是没有合并成有效的节目视频，请再点击本按钮一次以完成剩余任务。";
            }
            else
            {
                videoIndex = showResolutionDialog(GjsjVideoQualityList);
                downloadItems.Add(new DownloadItem(0, PatchFileExtension(videoName), "", MasterPlaylist.StreamInfos[videoIndex].Uri));///???
                btnStartDownload.ToolTip = null;
            }
            if (downloadItems.Count == 0)
            {
                CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, false, $"未能获取到{GjsjVideoQualityList[videoIndex]}视频的有效下载链接。");
                return;
            }
            taskProgress.TaskProgress = 100;
            downloadItems.ForEach(item => downloadItemList.Add(item));
            CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, false);
        }

        private int showResolutionDialog(List<string> videoQualities)
        {
            //2 选择分辨率
            QualitySelector dialog = new QualitySelector(false, videoQualities);
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Topmost = true;
            dialog.ShowDialog();
            //string videoDownloadUrl = urls[dialog.GetSelectionIndex()].url;
            //Log($"videoDownloadUrl={videoDownloadUrl}");
            //taskProgress.TaskProgress = 25;
            //return videoDownloadUrl;
            return dialog.GetSelectionIndex();
        }

        private (int, int) showVideoAudioQualitySelectionDialog(List<string> videoQualities, List<string> audioQualities)
        {
            var dialog = new VideoAudioQualitySelector(videoQualities, audioQualities);
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Topmost = true;
            dialog.ShowDialog();
            return dialog.GetSelectionIndex();
        }

        private Task<string> ParseGjsjVideoUrlAsyn(string url)
        {
            var task = Task.Run(async () =>
            {
                //1.获取视频连接网页代码
                Debug.WriteLine("第1步：" + System.DateTime.Now);
                string html = await GetHtmlCodeAsync(url);
                if (html.Length == 0)
                {
                    Debug.WriteLine("获取网页代码失败！");
                    CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, false, "获取视频播放网页代码失败！");
                    return "";
                }
                taskProgress.TaskProgress = 20;
                //2.master.m3u8
                Debug.WriteLine("第2步：" + System.DateTime.Now);
                File.WriteAllText(TMP_FILE, html);
                string m3u8Url = ParseGJSJMasterM3u8Url(TMP_FILE, out string videoName);
                if (m3u8Url.Length == 0)
                {
                    Debug.WriteLine("获取视频播放连接失败！");
                    CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, false, "获取视频播放m3u8文件连接失败！");
                    return "";
                }
                taskProgress.TaskProgress = 30;
                int pos = m3u8Url.LastIndexOf("/");
                if (pos == -1)
                {
                    Debug.WriteLine("视频连接解析出错！");
                    CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, false, "解析视频播放m3u8文件连接失败！");
                    return "";
                }
                taskProgress.TaskProgress = 40;
                //3.获取带分辨率的master.m3u8文件内容
                Debug.WriteLine("第3步：" + System.DateTime.Now);
                string m3u8Content = await GetHtmlCodeAsync(m3u8Url);
                if (m3u8Content.Length == 0)
                {
                    Debug.WriteLine("获取网页代码失败！");
                    CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, false, "获取视频多分辨率m3u8文件失败！");
                    return "";
                }
                taskProgress.TaskProgress = 60;
                //20240222
                VideoDL videoDL = proxyState.UseProxy ? new VideoDL(proxy: $"http://{proxyState.ProxyHost}:{proxyState.ProxyPort}") : new VideoDL();//proxy: "socks5://127.0.0.1:8000"
                var hlsDL = videoDL.Hls;
                MasterPlaylist = hlsDL.ParseMasterPlaylist(m3u8Content, m3u8Url);
                GjsjVideoQualityList.Clear();
                GjsjAudioQualityList.Clear();
                MasterPlaylist.StreamInfos.RemoveAll(item => item.Resolution == null);
                GjsjVideoQualityList = MasterPlaylist.StreamInfos.ConvertAll(item => item.Resolution.Height + "p");
                GjsjAudioQualityList = MasterPlaylist.MediaGroups.ConvertAll(item => item.Name);
                //urls = ParseGJSJVideoLink(m3u8Content, videoName);
                if (GjsjVideoQualityList.Count == 0)//urls.Count
                {
                    Debug.WriteLine("获取各视频下载连接失败！");
                    CtrolWidgetsOnTask(AppTask.TASK_FETCH_DOWNLOAD_URL, false, "解析视频多分辨率m3u8文件失败！");
                    return "";
                }
                //taskProgress.TaskProgress = 70;
                //4.
                /*string baseUrl = m3u8Url.Substring(0, pos + 1);
                urls.ForEach(url => url.url = baseUrl + url.url);
                for (int i = 0; i < urls.Count; i++)
                {
                    Debug.WriteLine(urls[i]);
                }
                Debug.WriteLine("第4步：" + System.DateTime.Now);*/
                taskProgress.TaskProgress = 80;
                return videoName;
            });

            return task;
        }

        private string ParseGJSJMasterM3u8Url(string htmlFile, out string title)
        {
            if (webSite == null)
            {
                title = "";
                return "";
            }
            var doc = new HtmlAgilityPack.HtmlDocument();
            //M1
            doc.LoadHtml(File.ReadAllText(htmlFile));
            //doc.Load(htmlCode);
            //M2
            //doc.Load(new FileStream(htmlFile, FileMode.Open));
            Log($"ParseGJSJMasterM3u8Url: 尝试使用 SelectorForUrl1 =“{webSite.SelectorForUrl1}” 解析网页文件");

            IList<HtmlNode> nodes;
            List<string> list = new List<string>();
            string m3u8Url = "";
            title = "";
            string[] selectors = webSite.SelectorForUrl1.Split("|");

            for (int i = 0; i < selectors.Length; i++)
            {
                nodes = doc.QuerySelectorAll(selectors[i].Trim());
                if (nodes.Count > 0)
                {
                    switch (i)
                    {
                        case 0:
                            GjsjM3u8Json? json;
                            foreach (HtmlNode node in nodes)
                            {
                                string html = node.InnerHtml.ToString().Replace("@", "");
                                json = JsonConvert.DeserializeObject<GjsjM3u8Json>(html);
                                if (json == null || json.contentUrl.Length == 0)
                                {
                                    continue;
                                }
                                m3u8Url = json.contentUrl;
                                System.Diagnostics.Debug.WriteLine(json.contentUrl);
                                System.Diagnostics.Debug.WriteLine("------------");
                                break;//找到第一个就可以了
                            }
                            break;
                        case 1:
                            GjsjM3u8Json20250115? json20250115;
                            foreach (HtmlNode node in nodes)
                            {
                                string html = node.InnerHtml.ToString().Replace("@", "");
                                json20250115 = JsonConvert.DeserializeObject<GjsjM3u8Json20250115>(html);
                                if (json20250115?.props?.pageProps?.video == null || string.IsNullOrEmpty(json20250115.props.pageProps.video.video_Url))
                                {
                                    continue;
                                }
                                m3u8Url = json20250115.props.pageProps.video.video_Url;
                                System.Diagnostics.Debug.WriteLine(json20250115.props.pageProps.video.video_Url);
                                System.Diagnostics.Debug.WriteLine("------------");
                                break;//找到第一个就可以了
                            }
                            break;
                    }
                    if (!string.IsNullOrEmpty(m3u8Url)) break;
                }
            }
            if (string.IsNullOrEmpty(m3u8Url))
            {
                Log($"ParseGJSJMasterM3u8Url: 尝试使用 SelectorForUrl2 =“{webSite.SelectorForUrl2}” 解析网页文件");
                nodes = doc.QuerySelectorAll(webSite.SelectorForUrl2);
                if (nodes.Count == 0)
                {
                    Debug.WriteLine("没有找到有效的解析单元……");
                    return "";
                }
                m3u8Url = nodes.First().FirstChild.GetAttributeValue("src", "");
            }
            if (m3u8Url.Length == 0)
            {
                Debug.WriteLine("没有找到 m3u8 的下载连接……");
                return "";
            }
            title = doc.QuerySelector("head title").InnerText;
            title = PatchTitle(title);
            return m3u8Url;
        }
        #endregion

        /////////////////////////////////////////////////////
        ///13. 测试 
        #region
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
        }
        #endregion

    }
}
