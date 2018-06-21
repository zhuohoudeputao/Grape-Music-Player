using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Grape_Music_Player
{
    public class LyricItem
    {
        //每一句歌词是一个LyricItem，包括歌词文本、持续时间、垂直偏移量

        //系统中有一个计时器，当计时器的时间超过该句的持续时间时就开始一段动画移动到下一句

        public string Sentence { get; set; }//歌词文本
        public double Time { get; set; }//持续时间
        public double TranslateY { get; set; }//歌词距离顶部的垂直偏移量
        public LyricItem(string sentence, double time)
        {
            Sentence = sentence;
            Time = time;
            TranslateY = 0;
        }
    }

    //歌词面板，是继承自Grid的一个自定义控件
    class LyricPanel : Grid
    {
        private List<LyricItem> Lyric;

        private double LINEHEIGHT;
        private int index = 0;
        private int currentLyricPos = 0;
        public int CenterToTop { get; set; }

        public LyricPanel()
        {
            LINEHEIGHT = 30;
            CenterToTop = 3;
            RenderTransform = new TranslateTransform();
        }

        public void Clear()//清空歌词和歌词面板
        {
            if (Lyric != null)
            {
                Lyric.Clear();
            }
            if (this.Children.Count > 0)
            {
                this.Children.Clear();
            }
        }

        public void SetSource(List<LyricItem> list, string title)//利用已有的lyric来设置初始内容，将每句歌词放到一个TextBlock里面并设置Margin值
        {
            if (this.Children.Count > 0)//如果已经有歌词了就先清空
            {
                this.Children.Clear();
            }

            TextBlock ti = new TextBlock
            {
                //Text = title,
                Text="",
                FontSize = 16,
                Margin = new Thickness(0, (CenterToTop - 1.5) * LINEHEIGHT, 0, LINEHEIGHT),//歌曲名放在第一行
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            this.Children.Add(ti);

            this.Height = (list.Count + CenterToTop + 1) * LINEHEIGHT;//歌词面板的高度设置
            int j = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                TextBlock tbk = new TextBlock()//存放歌词的文本块
                {
                    LineHeight = LINEHEIGHT,
                    Text = item.Sentence,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, (i + CenterToTop) * LINEHEIGHT, 0, LINEHEIGHT),//  j +
                    HorizontalAlignment = HorizontalAlignment.Center,
                    //距离上面是第几句歌词加上上面的歌词占的行数加上额外的行数，距离下面是一个行高
                };
                item.TranslateY = -(i) * LINEHEIGHT;//歌词块的垂直偏移量（即与“顶部”的距离）//+ j
                this.Children.Add(tbk);//把歌词块放进歌词面板
                j += GetTextBlockRowCount(tbk);//j是这句之前的歌词占了多少行
            }
            this.Lyric = list;//把歌词面板上的歌词设置为已有的lyric以便以后使用
        }

        private int GetTextBlockRowCount(TextBlock tbk)//获取一个歌词占用了多少行
        {
            return (int)(tbk.ActualWidth / tbk.Width);
        }

        public void UpdatePosition(TimeSpan position)//根据歌曲的播放进度改变歌词的位置，包括改变字体和执行动画
        {
            if (Lyric == null || Lyric.Count == 0) { return; }//如果没有歌词就返回
            index = GetCurrentIndex(position);
            if (index == currentLyricPos) { return; }
            if (index >= Children.Count) { return; }
            currentLyricPos = index;
            for (int i = 0; i < Children.Count; i++)
            {
                if (i != index)
                {
                    var other = Children[i] as TextBlock;
                    other.FontWeight = FontWeights.Normal;
                    other.Foreground = Brushes.White;
                }
            }
            var current = Children[index] as TextBlock;
            current.FontWeight = FontWeights.Bold;
            current.Foreground = Brushes.LightGreen;
            if (index != Lyric.Count)
            {
                Storyboard sb = new Storyboard();
                sb.Children.Add(GetTimeline(Lyric[index].TranslateY));
                sb.Begin();
            }
        }

        private DoubleAnimationUsingKeyFrames GetTimeline(double y)//执行的动画：整体上移一个偏移量
        {
            DoubleAnimationUsingKeyFrames daTranslate = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTargetProperty(daTranslate, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));//(UIElement.RenderTransform).(CompositeTransform.TranslateY)
            Storyboard.SetTarget(daTranslate, this);
            DiscreteDoubleKeyFrame keyframe2_1 = new DiscreteDoubleKeyFrame()
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)),
                Value = y + LINEHEIGHT
            };
            DiscreteDoubleKeyFrame keyframe2_2 = new DiscreteDoubleKeyFrame()
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4)),
                Value = y + LINEHEIGHT
            };
            EasingDoubleKeyFrame keyframe2_3 = new EasingDoubleKeyFrame()
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8)),
                Value = y
            };
            keyframe2_3.EasingFunction = new CubicEase()
            {
                EasingMode = EasingMode.EaseOut
            };
            daTranslate.KeyFrames.Add(keyframe2_1);
            daTranslate.KeyFrames.Add(keyframe2_2);
            daTranslate.KeyFrames.Add(keyframe2_3);
            return daTranslate;
        }

        private int GetCurrentIndex(TimeSpan ts)//获取当前走到了第几句
        {
            double sum = 0;
            int i = 0;
            for (; i < Lyric.Count; i++)
            {
                sum = sum + Lyric[i].Time;
                if (sum > ts.TotalSeconds)
                    break;
            }
            return i;
        }
    }
}