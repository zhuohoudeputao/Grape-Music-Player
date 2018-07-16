using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Grape_Music_Player
{
    class CoverObtainer
    {
        private string path;
        private double totalTime;
        private string coverPath;
        private string title;
        private string artist;
        private BitmapSource cover;
        public CoverObtainer(string Path, double TotalTime, string Title, string Artist)
        {
            path = Path;
            totalTime = TotalTime;
            title = Title;
            artist = Artist;
        }

        public CoverObtainer()
        {

        }

        public void SetPara(string Path, double TotalTime, string Title, string Artist)
        {
            path = Path;
            totalTime = TotalTime;
            title = Title;
            artist = Artist;
        }

        public BitmapSource GetCover()
        {
            cover = new BitmapImage(new Uri(@"\background.png", UriKind.Relative));
            coverPath = String.Format("Covers\\" + path.Substring(path.LastIndexOf("\\") + 1, path.LastIndexOf(".") - path.LastIndexOf("\\") - 1) + ".jpg");
            if (File.Exists(@coverPath))//歌曲文件所在的路径有歌词，就直接写入
            {
                Bitmap bmp = new Bitmap(coverPath);
                BitmapSource bmpResource = Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(),
                            IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                cover = bmpResource;
            }
            else
            {
                GetFromNetease();
            }
            return cover;
        }

        private bool GetFromNetease()
        {
            try
            {
                Uri Request = new Uri("http://music.163.com/api/search/pc?" + "s=" + title + "&type=1&limit=10");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Request);
                request.Method = "POST";
                request.ContentType = "json";
                request.Host = "music.163.com";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                JObject jo = JObject.Parse(reader.ReadToEnd());
                if ((int)jo["code"] == 200)
                {
                    string picUrl = "";
                    bool found = false;
                    foreach (var item in jo["result"]["songs"])
                    {
                        if ((string)item["artists"][0]["name"] == artist)
                        {
                            picUrl= (string)item["album"]["picUrl"];
                            found = true;
                            break;
                        }
                    }
                    if (found == false)
                        return false;
                    WebRequest imgRequest = WebRequest.Create(picUrl);
                    try
                    {
                        response = (HttpWebResponse)request.GetResponse();
                        Image downImage = Image.FromStream(imgRequest.GetResponse().GetResponseStream());
                        Bitmap bmp = new Bitmap(downImage);
                        BitmapSource bmpResource = Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(),
                            IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        cover = bmpResource;

                        string Name = path.Substring(path.LastIndexOf("\\") + 1, path.LastIndexOf(".") - path.LastIndexOf("\\") - 1);
                        if (!Directory.Exists("Covers\\"))
                            Directory.CreateDirectory("Covers\\");
                        string LocalPath = String.Format("Covers\\" + Name + ".jpg");
                        downImage.Save(LocalPath);
                        coverPath = LocalPath;
                        return true;
                    }
                    catch (WebException)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Newtonsoft.Json.JsonReaderException) { return false; }
            catch (WebException) { return false; }
            catch (NullReferenceException) { return false; }
        }
    }
}
