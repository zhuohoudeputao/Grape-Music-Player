using ID3;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grape_Music_Player
{
    class LyricObtainer
    {
        private string path;
        private double totalTime;
        private string lyricPath;
        private List<LyricItem> lyric;
        public LyricObtainer(string Path,double TotalTime)
        {
            path = Path;
            totalTime=TotalTime;
        }

        public LyricObtainer()
        {

        }

        public void SetPara(string Path, double TotalTime)
        {
            path = Path;
            totalTime = TotalTime;
        }

        public List<LyricItem> GetLyric()
        {
            lyric = new List<LyricItem>();
            //string path = player.CurrentSongAddress.LocalPath;
            lyricPath = path.Substring(0, path.LastIndexOf('.')) + ".lrc";
            if (File.Exists(@lyricPath))//歌曲文件所在的路径有歌词，就直接写入
            {
                WriteInLyricContent(Encoding.Default);
            }
            else//把路径切换到Lyrics文件夹下查找
            {
                lyricPath = String.Format("Lyrics\\" + path.Substring(path.LastIndexOf("\\") + 1, path.LastIndexOf(".") - path.LastIndexOf("\\") - 1) + ".lrc");
                if (File.Exists(@lyricPath))//这里还找不到才上网找
                {
                    WriteInLyricContent(Encoding.UTF8);
                }
                else
                {
                    if (GetFromNetease()==false && GetFromBaidu()==false && GetFromGecime() == false )//如果获取得到的话歌词已经被写在lyric里面了
                    {
                        lyric.Add(new LyricItem("找不到歌词", totalTime));
                    }
                }
            }

            return lyric;

        }

        private bool GetFromNetease()
        {
            try
            {
                ID3Info File = new ID3Info(path, true);
                string title = File.ID3v2Info.GetTextFrame("TIT2");
                string artist = File.ID3v2Info.GetTextFrame("TPE1");
                Uri Request = new Uri("http://music.163.com/api/search/pc?" + "s=" + title + "&type=1&limit=10");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Request);
                request.Method = "POST";
                request.ContentType = "json";
                request.Host = "music.163.com";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                JObject jo = JObject.Parse(reader.ReadToEnd());

                JObject jo2 = new JObject();
                if ((int)jo["code"] == 200)
                {
                    string id = "";
                    bool found = false;
                    foreach (var item in jo["result"]["songs"])
                    {
                        if ((string)item["artists"][0]["name"] == artist && (string)item["name"] == title)
                        {
                            id = (string)item["id"];
                            found = true;
                            break;
                        }
                    }
                    if (found == false)
                        return false;
                    Request = new Uri("http://music.163.com/api/song/lyric?os=pc&id=" + id + "&lv=-1");
                    request = (HttpWebRequest)WebRequest.Create(Request);
                    request.Method = "GET";
                    request.ContentType = "json";
                    try
                    { response = (HttpWebResponse)request.GetResponse(); }
                    catch (WebException)
                    {
                        return false;
                    }
                    reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                    jo2 = JObject.Parse(reader.ReadToEnd());
                    if (jo2["lrc"] == null || (string)jo2["lrc"]["lyric"] == null)
                        return false;
                    string lyricInJson = (string)jo2["lrc"]["lyric"];

                    string Name = path.Substring(path.LastIndexOf("\\") + 1, path.LastIndexOf(".") - path.LastIndexOf("\\") - 1);
                    if (!Directory.Exists("Lyrics\\"))
                        Directory.CreateDirectory("Lyrics\\");

                    string LocalPath = String.Format("Lyrics\\" + Name + ".lrc");

                    string[] seperator = { "\n" };
                    string[] lrcInJson = lyricInJson.Split(seperator, StringSplitOptions.None);
                    System.IO.File.WriteAllLines(LocalPath, lrcInJson);

                    lyricPath = LocalPath;
                    WriteInLyricContent(Encoding.UTF8);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Newtonsoft.Json.JsonReaderException){return false;}
            catch (WebException){return false;}
            catch(NullReferenceException){return false;}

        }

        private bool GetFromBaidu()
        {
            try
            {
                ID3Info File = new ID3Info(path, true);
                string title = File.ID3v2Info.GetTextFrame("TIT2");
                string artist = File.ID3v2Info.GetTextFrame("TPE1");
                Uri Request = new Uri("http://tingapi.ting.baidu.com/v1/restserver/ting?method=baidu.ting.search.catalogSug&query=" + title + " - " + artist);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Request);
                request.Method = "GET";
                request.ContentType = "json";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                JObject jo = JObject.Parse(reader.ReadToEnd());
                JObject jo2 = new JObject();
                if ((int)jo["error_code"] == 22000)
                {
                    string songId = (string)jo["song"][0]["songid"];
                    Request = new Uri("http://music.baidu.com/data/music/links?songIds=" + songId);
                    request = (HttpWebRequest)WebRequest.Create(Request);
                    request.Method = "GET";
                    request.ContentType = "json";
                    response = (HttpWebResponse)request.GetResponse();
                    
                    reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                    jo2 = JObject.Parse(reader.ReadToEnd());

                    string DownloadUrl = (string)jo2["data"]["songList"][0]["lrcLink"];
                    if (DownloadUrl == "")
                        return false;
                    string Name = path.Substring(path.LastIndexOf("\\") + 1, path.LastIndexOf(".") - path.LastIndexOf("\\") - 1);
                    if (!Directory.Exists("Lyrics\\"))
                        Directory.CreateDirectory("Lyrics\\");

                    string LocalPath = String.Format("Lyrics\\" + Name + ".lrc");

                    HttpWebRequest downloadRequest = (HttpWebRequest)WebRequest.Create(DownloadUrl);
                    HttpWebResponse downloadResponse = downloadRequest.GetResponse() as HttpWebResponse;
                    Stream responseStream = downloadResponse.GetResponseStream();
                    Stream stream = new FileStream(LocalPath, FileMode.Create);
                    byte[] bArr = new byte[1024];
                    int size = responseStream.Read(bArr, 0, bArr.Length);
                    while (size > 0)
                    {
                        stream.Write(bArr, 0, size);
                        size = responseStream.Read(bArr, 0, bArr.Length);
                    }
                    stream.Close();
                    responseStream.Close();

                    lyricPath = LocalPath;
                    WriteInLyricContent(Encoding.UTF8);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (WebException){return false;}
            catch (Newtonsoft.Json.JsonReaderException) { return false; }
        }

        private bool GetFromGecime()
        {
            try
            {
                ID3Info File = new ID3Info(path, true);
                string title = File.ID3v2Info.GetTextFrame("TIT2");
                string artist = File.ID3v2Info.GetTextFrame("TPE1");
                Uri Request = new Uri("http://geci.me/api/lyric/" + title + "/" + artist);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Request);
                request.Method = "GET";
                request.ContentType = "json";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);

                JObject jo = JObject.Parse(reader.ReadToEnd());

                string DownloadUrl = "";
                if ((int)jo["count"] >= 1)
                {
                    DownloadUrl = (string)jo["result"][0]["lrc"];
                }
                else
                {
                    //lyric.Add(new LyricItem("找不到歌词", totalTime));
                    return false;
                }
                string Name = path.Substring(path.LastIndexOf("\\") + 1, path.LastIndexOf(".") - path.LastIndexOf("\\") - 1);
                if (!Directory.Exists("Lyrics\\"))
                    Directory.CreateDirectory("Lyrics\\");

                string LocalPath = String.Format("Lyrics\\" + Name + ".lrc");

                HttpWebRequest downloadRequest = (HttpWebRequest)WebRequest.Create(DownloadUrl);
                HttpWebResponse downloadResponse = downloadRequest.GetResponse() as HttpWebResponse;
                Stream responseStream = downloadResponse.GetResponseStream();
                Stream stream = new FileStream(LocalPath, FileMode.Create);
                byte[] bArr = new byte[1024];
                int size = responseStream.Read(bArr, 0, bArr.Length);
                while (size > 0)
                {
                    stream.Write(bArr, 0, size);
                    size = responseStream.Read(bArr, 0, bArr.Length);
                }
                stream.Close();
                responseStream.Close();

                lyricPath = LocalPath;
                WriteInLyricContent(Encoding.UTF8);
                return true;
            }
            catch (WebException){return false;}
            catch (Newtonsoft.Json.JsonReaderException) { return false; }
        }

        private void WriteInLyricContent(Encoding encoding)
        {
            string lyricContent = File.ReadAllText(lyricPath, encoding);

            List<string> Timespans = new List<string>();
            string[] arr = lyricContent.Split(new char[] { '\r', '\n' });
            Regex regex = new Regex(@"\[\d{2}:\d{2}(.\d{2})*\]");
            if (arr != null && arr.Length > 0)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var str = arr[i].Replace("by", "")
                        .Replace("all", "")
                        .Replace("ti", "")
                        .Replace("offset", "");
                    var matches = regex.Matches(str);
                    if (matches != null && matches.Count > 0)
                    {
                        for (int j = 0; j < matches.Count; j++)
                        {
                            Timespans.Add(matches[j].Value);
                        }
                    }
                }
            }
            regex = new Regex(@"\[\d{2}:\d{2}(.\d{3})*\]");
            if (arr != null && arr.Length > 0)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var str = arr[i].Replace("by", "")
                        .Replace("all", "")
                        .Replace("ti", "")
                        .Replace("offset", "");
                    var matches = regex.Matches(str);
                    if (matches != null && matches.Count > 0)
                    {
                        for (int j = 0; j < matches.Count; j++)
                        {
                            Timespans.Add(matches[j].Value);
                        }
                    }
                }
            }
            Timespans.Sort();//排序
            Timespans.Distinct();//去掉重复元素


            for (int i = 0; i < Timespans.Count; i++)
            {
                for (int j = 0; j < arr.Length; j++)
                {
                    var index = arr[j].IndexOf(Timespans[i]);
                    if (index > -1)
                    {
                        string sentence = arr[j].Substring(arr[j].LastIndexOf(']') + 1).TrimEnd('\r').Trim();
                        double time = 0;
                        if (i == 0)
                            time = GetSeconds(Timespans[i]);
                        else
                            time = GetSeconds(Timespans[i]) - GetSeconds(Timespans[i - 1]);
                        lyric.Add(new LyricItem(sentence, time));
                    }
                }
            }
        }

        private double GetSeconds(string timespan)
        {
            timespan = timespan.Replace("[", "").Replace("]", "");
            string minute = timespan.Substring(0, 2);
            string second = timespan.Substring(3);
            return Convert.ToDouble(minute) * 60 + Convert.ToDouble(second);
        }
    }
}
