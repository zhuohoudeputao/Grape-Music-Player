using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Grape_Music_Player.AeroWindow;
using ID3;
using ID3.ID3v2Frames.BinaryFrames;
using Newtonsoft.Json.Linq;

namespace Grape_Music_Player
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        Player player = new Player();
        private DispatcherTimer TitleTimer = new DispatcherTimer();
        private DispatcherTimer LyricTimer = new DispatcherTimer();
        private DispatcherTimer MusicProcessTimer = new DispatcherTimer();

        /// <summary>
        /// 注册快捷集合
        /// </summary>
        readonly Dictionary<string, short> hotKeyDic = new Dictionary<string, short>();

        public MainWindow()
        {
            InitializeComponent();

            using (SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }

                int availablenum = 0;
                if (Properties.Settings.Default.IsFirstRun==true)
                {
                    //首先删除数据库中失效的歌曲，同时统计出有效的歌曲数
                    List<string> delete = new List<string>();
                    SqlCommand sqlCmd = new SqlCommand("SELECT Address from MUSIC", conn);
                    try
                    {
                        SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                        while (sqlDataReader.Read())
                        {
                            if (File.Exists(sqlDataReader[0].ToString())) availablenum++;
                            else delete.Add(sqlDataReader[0].ToString());
                        }
                        sqlDataReader.Close();
                    }
                    catch (InvalidOperationException) { }

                    SqlCommand deletecmd = new SqlCommand();
                    foreach (string d in delete)
                    {
                        deletecmd = new SqlCommand(string.Format("DELETE from Music WHERE Address=N'{0}'", d), conn);
                        deletecmd.ExecuteNonQuery();
                    }

                    Properties.Settings.Default.IsFirstRun = false;
                    Properties.Settings.Default.Save();

                }
                else//第二次运行就不用先遍历整个数据库了，只需要找到两首能用的歌就可以开始运行
                {
                    SqlCommand sqlCmd = new SqlCommand("SELECT Address from MUSIC", conn);
                    try
                    {
                        SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                        while (sqlDataReader.Read())
                        {
                            if (File.Exists(sqlDataReader[0].ToString()))
                            {
                                availablenum++;
                                if (availablenum >= 2)
                                    break;
                            }
                        }
                        sqlDataReader.Close();
                    }
                    catch (InvalidOperationException) { }
                }

                while (availablenum < 2)
                {
                    MessageBox.Show("您尚未添加歌曲或歌曲量不足，请选择文件夹以添加歌曲");
                    AddButton_Click(this, null);
                    SqlCommand sqlCmd = new SqlCommand("SELECT COUNT(*) from MUSIC", conn);
                    SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                    sqlDataReader.Read();
                    availablenum = (int)sqlDataReader[0];
                    sqlDataReader.Close();
                }
                conn.Close();
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new LongTimeDelegate(DeleteUselessSongs));

            player.MusicChange += Player_MusicChange;
            player.MusicNeeded += Player_MusicNeeded;
            player.MusicPlayed += Player_MusicPlayed;

            TitleTimer.Tick += TitleTimer_Tick;
            TitleTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);

            LyricTimer.Tick += LyricTimer_Tick;
            LyricTimer.Interval = new TimeSpan(0, 0, 1);

            MusicProcessTimer.Tick += MusicProcessTimer_Tick;
            MusicProcessTimer.Interval = new TimeSpan(0, 0, 1);

            Loaded += (sender, e) =>
            {
                var wpfHwnd = new WindowInteropHelper(this).Handle;

                var hWndSource = HwndSource.FromHwnd(wpfHwnd);
                //添加处理程序
                if (hWndSource != null) hWndSource.AddHook(MainWindowProc);

                hotKeyDic.Add("F5", Win32.GlobalAddAtom("F5"));
                hotKeyDic.Add("F6", Win32.GlobalAddAtom("F6"));
                hotKeyDic.Add("PlayPause", Win32.GlobalAddAtom("PlayPause"));
                hotKeyDic.Add("NextSong", Win32.GlobalAddAtom("NextSong"));
                Win32.RegisterHotKey(wpfHwnd, hotKeyDic["F5"], Win32.KeyModifiers.None, (int)System.Windows.Forms.Keys.F5);
                Win32.RegisterHotKey(wpfHwnd, hotKeyDic["F6"], Win32.KeyModifiers.None, (int)System.Windows.Forms.Keys.F6);
                Win32.RegisterHotKey(wpfHwnd, hotKeyDic["PlayPause"], Win32.KeyModifiers.None, (int)System.Windows.Forms.Keys.MediaPlayPause);
                Win32.RegisterHotKey(wpfHwnd, hotKeyDic["NextSong"], Win32.KeyModifiers.None, (int)System.Windows.Forms.Keys.MediaNextTrack);
            };

            player.NextSong();

            GarbageCollect();
        }

        /// <summary>
        /// 响应快捷键事件
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        private IntPtr MainWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case Win32.WmHotkey:
                    {
                        int sid = wParam.ToInt32();
                        if (sid == hotKeyDic["F5"]|| sid == hotKeyDic["PlayPause"])
                        {
                            PlayButton_Click(this, null);
                        }
                        else if (sid == hotKeyDic["F6"]|| sid == hotKeyDic["NextSong"])
                        {
                            NextButton_Click(this, null);
                        }
                        handled = true;
                        break;
                    }
            }

            return IntPtr.Zero;
        }

        private void DeleteUselessSongs()
        {
            using (SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                
                List<string> delete = new List<string>();
                SqlCommand sqlCmd = new SqlCommand("SELECT Address from MUSIC", conn);
                try
                {
                    SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                    while (sqlDataReader.Read())
                    {
                        if (!File.Exists(sqlDataReader[0].ToString()))
                            delete.Add(sqlDataReader[0].ToString());
                    }
                    sqlDataReader.Close();
                }
                catch (InvalidOperationException) { }

                SqlCommand deletecmd = new SqlCommand();
                foreach (string d in delete)
                {
                    deletecmd = new SqlCommand(string.Format("DELETE from Music WHERE Address=N'{0}'", d), conn);
                    deletecmd.ExecuteNonQuery();
                }

                conn.Close();
            }
        }

        private void MusicProcessTimer_Tick(object sender, EventArgs e)
        {
            MusicProcessSlider.Value = MusicProcessSlider.Maximum * player.GetProcess();
        }

        private void LyricTimer_Tick(object sender, EventArgs e)
        {
            lyricPanel.UpdatePosition(player.GetPosition());
        }

        private void TitleTimer_Tick(object sender, EventArgs e)
        {
            string title = TitleLabel.Content.ToString();
            TitleLabel.Content = title.Substring(1) + title[0];
        }

        private void Player_MusicPlayed()
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            string update = string.Format("UPDATE MUSIC SET Times=Times+1 WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath);
            SqlCommand updatecmd = new SqlCommand(update, conn);
            updatecmd.ExecuteNonQuery();

            conn.Close();
        }

        private void Player_MusicNeeded()
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            string str = "SELECT top 10 * from MUSIC ORDER BY newid()";
            SqlCommand sqlCmd = new SqlCommand(str, conn);
            SqlDataReader sqlDR = sqlCmd.ExecuteReader();
            while (sqlDR.Read())
            {
                player.NextSongAddress.Enqueue(new Uri((string)sqlDR[0]));
            }
            sqlDR.Close();
            conn.Close();
        }

        private delegate void LongTimeDelegate();
        private void Player_MusicChange()//UI更改
        {
            TitleTimer.Stop();
            LyricTimer.Stop();
            MusicProcessTimer.Stop();
            LoadPicture();
            //Dispatcher.BeginInvoke(DispatcherPriority.Background, new LongTimeDelegate(LoadPicture));
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new LongTimeDelegate(LoadUI));
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new LongTimeDelegate(LoadLyric));
            
            //LoadUI();
            //LoadLyric();

            MusicProcessTimer.Start();

            GarbageCollect();
        }

        #region 封面
        private CoverObtainer coverObtainer = new CoverObtainer();
        private void GetCover()
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            SqlCommand sqlCmd = new SqlCommand(string.Format("SELECT Title,Artist from MUSIC WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath), conn);
            SqlDataReader sqlDR = sqlCmd.ExecuteReader();
            sqlDR.Read();
            string title = sqlDR[0].ToString();
            string artist = sqlDR[1].ToString();
            coverObtainer.SetPara(player.CurrentSongAddress.LocalPath, player.GetMusicDuringTime().TotalSeconds, title, artist);
            BitmapSource image = coverObtainer.GetCover();
            BackgroundPicture.Source = image;
            AlbumPicture.Source = image;
        }
        private void LoadPicture()
        {
            string file = player.CurrentSongAddress.LocalPath;
            string Name = file.Substring(file.LastIndexOf("\\") + 1, file.LastIndexOf(".") - file.LastIndexOf("\\") - 1);
            if (!File.Exists(String.Format("Covers\\" + Name + ".jpg")))
            {
                ID3Info song = new ID3Info(file, true);
                foreach (AttachedPictureFrame AP in song.ID3v2Info.AttachedPictureFrames.Items)
                {
                    try
                    {
                        if (AP.Data != null && (AP.PictureType == AttachedPictureFrame.PictureTypes.Cover_Front || AP.PictureType == AttachedPictureFrame.PictureTypes.Media))
                        {
                            System.Drawing.Image image = System.Drawing.Image.FromStream(AP.Data);

                            if (!Directory.Exists("Covers\\"))
                                Directory.CreateDirectory("Covers\\");

                            image.Save(String.Format("Covers\\" + Name + ".jpg"));
                        }
                    }
                    catch (ArgumentException) { continue; }
                }
            }
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new LongTimeDelegate(GetCover));
        }
        #endregion
        private void LoadUI()
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            SqlCommand sqlCmd = new SqlCommand(string.Format("SELECT Loved,Name from MUSIC WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath), conn);
            SqlDataReader sqlDR = sqlCmd.ExecuteReader();
            sqlDR.Read();
            short Loved = (short)sqlDR[0];
            if (Loved == 1)
                LoveButton.Foreground = System.Windows.Media.Brushes.Red;
            else
                LoveButton.Foreground = System.Windows.Media.Brushes.White;


            string Title = (string)sqlDR[1];
            TitleLabel.Content = Title;
            UpdateLayout();//使得得到的是最新的ActualWidth
            if (Title.Length>44)
            {
                TitleLabel.Content = Title + "  ";//加个空格是为了美观
                TitleTimer.Start();
            }

            sqlDR.Close();
            conn.Close();
        }

        private void LoadLyric()
        {
            GetLyric();
            lyricPanel.SetSource(lyric, Title);
            LyricTimer.Start();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            System.Environment.Exit(0);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = fbd.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.Cancel) { return; }
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            string path = fbd.SelectedPath.Trim();
            string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".mp3") || s.EndsWith(".flac")).ToArray();
            foreach (string file in files)
            {
                if (file.Contains("'"))
                    continue;
                try
                {
                    string Name = file.Substring(file.LastIndexOf("\\") + 1, file.LastIndexOf(".") - file.LastIndexOf("\\") - 1);
                    ID3Info song = new ID3Info(file, true);
                    string title = song.ID3v2Info.GetTextFrame("TIT2");
                    string artist = song.ID3v2Info.GetTextFrame("TPE1");
                    SqlCommand sqlCmd = new SqlCommand(string.Format("INSERT INTO MUSIC(Address,Name,Title,Artist) VALUES(N'{0}',N'{1}',N'{2}',N'{3}')", file, Name, title, artist), conn);
                    sqlCmd.ExecuteNonQuery();
                }
                catch (SqlException)
                {
                    continue;
                }
                catch(OverflowException)
                {
                    continue;
                }
                catch(ArgumentException)
                {
                    continue;
                }
            }
            fbd.Dispose();
            conn.Close();
            GarbageCollect();
        }

        private void LoveButton_Click(object sender, RoutedEventArgs e)
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            string update = string.Format("UPDATE MUSIC SET Loved=-Loved+1 WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath);
            SqlCommand updatecmd = new SqlCommand(update, conn);
            updatecmd.ExecuteNonQuery();

            conn.Close();

            if (LoveButton.Foreground == System.Windows.Media.Brushes.White)
                LoveButton.Foreground = System.Windows.Media.Brushes.Red;
            else
                LoveButton.Foreground = System.Windows.Media.Brushes.White;

        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            player.ChangePlayState();
            if (player.playState == Player.PlayState.playing)
            {
                PlayButton.Content = "\uf04c";
            }
            else
            {
                PlayButton.Content = "\uf04b";
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            player.NextSong();
            PlayButton.Content = "\uf04c";
        }

        private delegate void LongTimeDelegate2(String arg);
        private void DeleteSingleSong(string address)
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            string del = string.Format("DELETE from Music WHERE Address=N'{0}'", address);
            SqlCommand deletecmd = new SqlCommand(del, conn);
            deletecmd.ExecuteNonQuery();
            conn.Close();

            File.Delete(address);
        }
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("删除该歌曲将会同时删除本地文件，确认删除？", "删除文件", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                player.Close();

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new LongTimeDelegate2(DeleteSingleSong), player.CurrentSongAddress.LocalPath);
                
                player.NextSong();
            }
        }

        #region 界面控制
        private void MinButton_Click(object sender, RoutedEventArgs e)
        {
            Window.WindowState = WindowState.Minimized;
        }

        private void TopButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.Topmost == true)
            {
                Window.Topmost = false;
                TopButton.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                Window.Topmost = true;
                TopButton.Foreground = System.Windows.Media.Brushes.LightGreen;
            }

        }

        #endregion

        private string AmendString(String str)
        {
            if (str.Contains("'"))
                return str.Replace("'", @"\'");
            else
                return str;
        }

        #region 歌词
        private List<LyricItem> lyric = new List<LyricItem>();
        private LyricObtainer lyricObtainer = new LyricObtainer();
        private void GetLyric()
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.MusicConnectionString);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            SqlCommand sqlCmd = new SqlCommand(string.Format("SELECT Title,Artist from MUSIC WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath), conn);
            SqlDataReader sqlDR = sqlCmd.ExecuteReader();
            sqlDR.Read();
            string title = sqlDR[0].ToString();
            string artist = sqlDR[1].ToString();
            lyricObtainer.SetPara(player.CurrentSongAddress.LocalPath, player.GetMusicDuringTime().TotalSeconds,title,artist);
            lyric = lyricObtainer.GetLyric();
        }
        #endregion

        #region 内存回收
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
        public static extern int SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);
        /// <summary>
        /// 释放内存
        /// </summary>
        public static void GarbageCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                MainWindow.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
            }
        }
        #endregion
    }

    
}
