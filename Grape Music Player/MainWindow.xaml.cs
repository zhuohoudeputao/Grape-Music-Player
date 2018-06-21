using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
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
        private DispatcherTimer TitleTimer=new DispatcherTimer();
        private DispatcherTimer LyricTimer = new DispatcherTimer();

        public MainWindow()
        {
            //RoutedEventHandler handler = null;
            //handler = (s, e) =>
            //{
            //    Loaded -= handler;
            //    this.EnableBlur();
            //};
            //Loaded += handler;

            InitializeComponent();

            SqlConnection conn = new SqlConnection(strConn);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            //首先检查歌曲的数量是否足够
            int availablenum = 0;
            
            List<string> delete = new List<string>();
            
            string str = "SELECT Address from MUSIC";
            SqlCommand sqlCmd = new SqlCommand(str, conn);
            do
            {
                availablenum = 0;
                SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                while (sqlDataReader.Read())
                {
                    if (File.Exists((string)sqlDataReader[0].ToString()))
                    {
                        availablenum++;
                    }
                    else
                    {
                        delete.Add((string)sqlDataReader[0]);
                    }
                }
                sqlDataReader.Close();

                foreach(string d in delete)
                {
                    string del = string.Format("DELETE from Music WHERE Address=N'{0}'", d);
                    SqlCommand deletecmd = new SqlCommand(del, conn);
                    deletecmd.ExecuteNonQuery();
                }
                
                if (availablenum < 2)
                {
                    MessageBox.Show("您尚未添加歌曲或歌曲量不足，请选择文件夹以添加歌曲");
                    AddButton_Click(this, null);
                }
            } while (availablenum < 2);

            //搞定之后随机选择两首音乐放入player中
            str = "SELECT top 2 * from MUSIC ORDER BY newid()";
            sqlCmd = new SqlCommand(str, conn);
            SqlDataReader sqlDR = sqlCmd.ExecuteReader();
            sqlDR.Read();
            player.CurrentSongAddress = new Uri((string)sqlDR[0]);
            sqlDR.Read();
            player.NextSongAddress = new Uri((string)sqlDR[0]);
            sqlDR.Close();

            conn.Close();

            player.MusicChange += Player_MusicChange;
            player.MusicNeeded += Player_MusicNeeded;
            player.MusicPlayed += Player_MusicPlayed;
            
            TitleTimer.Tick += TitleTimer_Tick;
            TitleTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);

            LyricTimer.Tick += LyricTimer_Tick;
            LyricTimer.Interval = new TimeSpan(0, 0, 1);

            try
            {
                player.Load(player.CurrentSongAddress);
                player.Play();
            }
            catch(IOException)
            {
                player.NextSong();
            }
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

            do
            {
                string str = "SELECT top 1 * from MUSIC ORDER BY newid()";
                SqlCommand sqlCmd = new SqlCommand(str, conn);
                SqlDataReader sqlDR = sqlCmd.ExecuteReader();
                sqlDR.Read();
                player.NextSongAddress = new Uri((string)sqlDR[0]);
                sqlDR.Close();
            } while (player.NextSongAddress == player.CurrentSongAddress);
            
            conn.Close();
        }

        private void Player_MusicChange()//UI更改
        {
            TitleTimer.Stop();
            LyricTimer.Stop();
            AlbumPicture.Source = new BitmapImage(new Uri(@"\background.png", UriKind.Relative));
            //AlbumPicture.Source = null;
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
            catch(OverflowException)
            {

            }
            

            SqlConnection conn = new SqlConnection(strConn);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            string str =string.Format( "SELECT Loved,Name from MUSIC WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath);
            SqlCommand sqlCmd = new SqlCommand(str, conn);
            SqlDataReader sqlDR = sqlCmd.ExecuteReader();
            sqlDR.Read();
            int Loved = (int)sqlDR[0];
            if (Loved==1)
                LoveButton.Foreground = System.Windows.Media.Brushes.Red;
            else
                LoveButton.Foreground = System.Windows.Media.Brushes.White;
            string Title = (string)sqlDR[1];
            
            TitleLabel.Content = Title;
            UpdateLayout();//使得得到的是最新的ActualWidth
            if (TitleLabel.ActualWidth >= TitleBar.ActualWidth&&TitleLabel.ActualWidth!=0)
            {
                TitleLabel.Content = Title + "  ";//加个空格是为了美观
                TitleTimer.Start();
            }
                
            sqlDR.Close();
            conn.Close();

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

            if (result == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }

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
            string[] Files = Directory.GetFiles(path, "*.mp3", SearchOption.AllDirectories);

            foreach (string File in Files)
            {
                if (File.Contains("'"))
                    continue;

                string Name = File.Substring(File.LastIndexOf("\\") + 1, File.LastIndexOf(".") - File.LastIndexOf("\\") - 1);

                string str = string.Format("SELECT * from MUSIC Where Address=N'{0}'", File);
                SqlCommand sqlCmd = new SqlCommand(str, conn);
                SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                if (!sqlDataReader.HasRows)
                {
                    sqlDataReader.Close();
                    str = string.Format("INSERT INTO MUSIC(Address,Name) VALUES(N'{0}',N'{1}')", File, Name);
                    sqlCmd = new SqlCommand(str, conn);
                    sqlCmd.ExecuteNonQuery();
                }
                sqlDataReader.Close();
            }

            Files = Directory.GetFiles(path, "*.flac", SearchOption.AllDirectories);
            foreach (string File in Files)
            {
                if (File.Contains("'"))
                    continue;

                string Name = File.Substring(File.LastIndexOf("\\") + 1, File.LastIndexOf(".") - File.LastIndexOf("\\") - 1);

                string str = string.Format("SELECT * from MUSIC Where Address=N'{0}'", File);
                SqlCommand sqlCmd = new SqlCommand(str, conn);
                SqlDataReader sqlDataReader = sqlCmd.ExecuteReader();
                if (!sqlDataReader.HasRows)
                {
                    sqlDataReader.Close();
                    str = string.Format("INSERT INTO MUSIC(Address,Name) VALUES(N'{0}',N'{1}')", File, Name);
                    sqlCmd = new SqlCommand(str, conn);
                    sqlCmd.ExecuteNonQuery();
                }
                sqlDataReader.Close();
            }

            conn.Close();
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
            if(result==MessageBoxResult.OK)
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

                string del = string.Format("DELETE from Music WHERE Address=N'{0}'", player.CurrentSongAddress.LocalPath);
                SqlCommand deletecmd = new SqlCommand(del, conn);
                deletecmd.ExecuteNonQuery();

                conn.Close();

                player.Close();
                File.Delete(player.CurrentSongAddress.LocalPath);
                player.NextSong();
            }
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            TitleBar.Visibility = Visibility.Visible;
            ControlBar.Visibility = Visibility.Visible;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            TitleBar.Visibility = Visibility.Hidden;
            ControlBar.Visibility = Visibility.Hidden;
        }

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

            lyricObtainer.SetPara(player.CurrentSongAddress.LocalPath, player.GetMusicDuringTime().TotalSeconds);
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

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Window.Width = Window.ActualHeight;
        }

        private void MinButton_Click(object sender, RoutedEventArgs e)
        {
            Window.WindowState = WindowState.Minimized;
        }
    }
}
