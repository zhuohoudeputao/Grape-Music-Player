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
        const string strConn = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\Music.mdf;Integrated Security=True;Connect Timeout=30";

        Player player = new Player();
        private DispatcherTimer TitleTimer = new DispatcherTimer();
        private DispatcherTimer LyricTimer = new DispatcherTimer();
        private DispatcherTimer MusicProcessTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();

            using (SqlConnection conn = new SqlConnection(strConn))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                
                    //首先删除数据库中失效的歌曲，同时统计出有效的歌曲数
                    int availablenum = 0;
                    List<string> delete = new List<string>();
                    SqlCommand sqlCmd = new SqlCommand("SELECT Address from MUSIC", conn);
                    try
                    {
                        SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                        while (sqlDataReader.Read())
                        {
                            if (File.Exists(sqlDataReader[0].ToString()))
                            {
                                availablenum++;
                            }
                            else
                            {
                                delete.Add(sqlDataReader[0].ToString());
                            }
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

                    //删完歌曲后添加歌曲直到歌曲数超过2首
                    while (availablenum < 2)
                    {
                        MessageBox.Show("您尚未添加歌曲或歌曲量不足，请选择文件夹以添加歌曲");
                        AddButton_Click(this, null);
                        sqlCmd = new SqlCommand("SELECT COUNT(*) from MUSIC", conn);
                        SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                        sqlDataReader.Read();
                        availablenum = (int)sqlDataReader[0];
                        sqlDataReader.Close();
                    }
                    
                conn.Close();
            }

            player.MusicChange += Player_MusicChange;
            player.MusicNeeded += Player_MusicNeeded;
            player.MusicPlayed += Player_MusicPlayed;

            TitleTimer.Tick += TitleTimer_Tick;
            TitleTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);

            LyricTimer.Tick += LyricTimer_Tick;
            LyricTimer.Interval = new TimeSpan(0, 0, 1);

            MusicProcessTimer.Tick += MusicProcessTimer_Tick;
            MusicProcessTimer.Interval = new TimeSpan(0, 0, 1);

            player.NextSong();

            GarbageCollect();
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
            SqlConnection conn = new SqlConnection(strConn);
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
            SqlConnection conn = new SqlConnection(strConn);
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

        private void LoadPicture()
        {
            AlbumPicture.Source = new BitmapImage(new Uri(@"\background.png", UriKind.Relative));
            try
            {
                ID3Info info = new ID3Info(player.CurrentSongAddress.LocalPath, true);
                foreach (AttachedPictureFrame AP in info.ID3v2Info.AttachedPictureFrames.Items)
                {
                    if (AP.Data != null && (AP.PictureType == AttachedPictureFrame.PictureTypes.Cover_Front || AP.PictureType == AttachedPictureFrame.PictureTypes.Media))
                    {
                        System.Drawing.Image image = System.Drawing.Image.FromStream(AP.Data);
                        Bitmap bmp = new Bitmap(image);
                        BitmapSource bmpResource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(),
                            IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        AlbumPicture.Source = bmpResource;
                    }
                }
            }
            catch (OverflowException) { }
            catch (ArgumentException) { }
        }

        private void LoadUI()
        {
            SqlConnection conn = new SqlConnection(strConn);
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
            if (TitleLabel.ActualWidth >= TitleBar.ActualWidth && TitleLabel.ActualWidth != 0)
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
            SqlConnection conn = new SqlConnection(strConn);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            string path = fbd.SelectedPath.Trim();
            string[] Files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".mp3") || s.EndsWith(".flac")).ToArray();
            foreach (string File in Files)
            {
                if (File.Contains("'"))
                    continue;
                try
                {
                    string Name = File.Substring(File.LastIndexOf("\\") + 1, File.LastIndexOf(".") - File.LastIndexOf("\\") - 1);
                    ID3Info file = new ID3Info(File, true);
                    string title = file.ID3v2Info.GetTextFrame("TIT2");
                    string artist = file.ID3v2Info.GetTextFrame("TPE1");
                    SqlCommand sqlCmd = new SqlCommand(string.Format("INSERT INTO MUSIC(Address,Name,Title,Artist) VALUES(N'{0}',N'{1}',N'{2}',N'{3}')", File, Name,title,artist), conn);
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
            }
            fbd.Dispose();
            conn.Close();
            GarbageCollect();
        }

        private void LoveButton_Click(object sender, RoutedEventArgs e)
        {
            SqlConnection conn = new SqlConnection(strConn);
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

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("删除该歌曲将会同时删除本地文件，确认删除？", "删除文件", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                player.Close();

                SqlConnection conn = new SqlConnection(strConn);
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                string del = string.Format("DELETE from Music WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath);
                SqlCommand deletecmd = new SqlCommand(del, conn);
                deletecmd.ExecuteNonQuery();
                conn.Close();
                
                File.Delete(player.CurrentSongAddress.LocalPath);
                player.NextSong();
            }
        }

        #region 界面控制
        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            TitleBar.Visibility = Visibility.Visible;
            ControlBar.Visibility = Visibility.Visible;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            TitleBar.Visibility = Visibility.Hidden;
            ControlBar.Visibility = Visibility.Collapsed;
        }

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
            SqlConnection conn = new SqlConnection(strConn);
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

        private void LyricButton_Click(object sender, RoutedEventArgs e)
        {
            if (LyricPanel.Visibility == Visibility.Visible)
            {
                LyricPanel.Visibility = Visibility.Collapsed;
                LyricCoverRect.Visibility = Visibility.Collapsed;
                LyricButton.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                LyricPanel.Visibility = Visibility.Visible;
                LyricCoverRect.Visibility = Visibility.Visible;
                LyricButton.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
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
