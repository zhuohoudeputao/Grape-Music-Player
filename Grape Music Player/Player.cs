using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Grape_Music_Player
{
    class Player:MediaPlayer
    {
        public Uri CurrentSongAddress
        { get; set; }
        public Queue<Uri> NextSongAddress = new Queue<Uri>();

        #region 播放状态(播放中、暂停)
        public enum PlayState : int
        {
            playing = 0,
            paused = 1
        }
        public PlayState playState = PlayState.playing;
        public void ChangePlayState()
        {
            if (playState == PlayState.playing)
            {
                Pause();
                playState = PlayState.paused;
            }
            else
            {
                Play();
                playState = PlayState.playing;
            }
        }
        #endregion

        #region 相关事件
        public delegate void MyEventHandler();
        public event MyEventHandler MusicChange;//音乐改变，用于改变UI
        public event MyEventHandler MusicNeeded;//需要音乐，用于加入新的NextSong
        public event MyEventHandler MusicPlayed;//音乐播放完毕，用于增加歌曲的Times字段
        #endregion

        #region 构造函数
        public Player()
        {
            Volume = 1;
            MediaEnded += AutoNextSong;
        }
        public Player(Uri File) : this()
        {
            Load(File);
        }
        public void Load(Uri File)
        {
            Close();
            CurrentSongAddress = File;
            Open(CurrentSongAddress);
            MusicChange();
        }
        #endregion

        #region 播放控制：播放、暂停、下一曲、自动播放下一曲
        public new void Play()
        {
            base.Play();
            playState = PlayState.playing;
        }
        public new void Pause()
        {
            base.Pause();
            playState = PlayState.paused;
        }
        
        public void NextSong()
        {
            Close();
            if (NextSongAddress.Count < 2)
                MusicNeeded();
            try
            {
                Load(NextSongAddress.Dequeue());
                Play();
            }
            catch(IOException)
            {
                if(NextSongAddress.Count < 2)
                    MusicNeeded();
                NextSong();
            }
        }
        private void AutoNextSong(object sender, EventArgs e)
        {
            MusicPlayed();
            NextSong();
        }
        #endregion

        #region 时间与进度相关设置
        public TimeSpan GetMusicDuringTime()
        {
            if (base.NaturalDuration.HasTimeSpan)
                return base.NaturalDuration.TimeSpan;
            else
                return new TimeSpan(0, 0, 0);
        }
        public void SetPosition(TimeSpan tp)
        {
            base.Position = tp;
        }
        public TimeSpan GetPosition()
        {
            return base.Position;
        }
        public double GetProcess()
        {
            if (GetMusicDuringTime().TotalSeconds == 0) return 0; 
            return GetPosition().TotalSeconds / GetMusicDuringTime().TotalSeconds;
        }
        #endregion
    }
}
