using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;

namespace Adjutant
{
    class Chunk
    {
        string text, link;
        bool newline, absNewline, mouseOver, strikeout;
        Rectangle bounds;
        SolidBrush brush;
        Image img;


        public Chunk(string text, string link, Color color, bool strikeout, bool newline, bool absNewline, int w, int h)
        {
            this.text = text;
            this.link = link;
            this.strikeout = strikeout;
            this.newline = newline;
            this.absNewline = absNewline;

            brush = new SolidBrush(color);
            bounds = new Rectangle(0, 0, w, h);
        }

        public Chunk(string imgURL, string link, bool newline, bool absNewline)
        {
            if (File.Exists(imgURL))
                img = Image.FromFile(imgURL);
            else
                img = downloadImage(imgURL);

            //img.Save(@"C:\Users\Winterstark\Desktop\Sheever.png");

            this.link = link;
            this.newline = newline;
            this.absNewline = absNewline;

            bounds = new Rectangle(0, 0, img.Width, img.Height);

            text = "";
            strikeout = false;
            brush = new SolidBrush(Color.White);
        }

        public override string ToString()
        {
            return text;
        }

        public string GetText()
        {
            //includes newline
            return text + (newline ? Environment.NewLine : "");
        }

        public bool IsNewline()
        {
            return newline;
        }

        public bool IsAbsNewline()
        {
            return absNewline;
        }

        public void InsertNewline()
        {
            newline = true;
            absNewline = true;
        }

        public Color GetColor()
        {
            return brush.Color;
        }

        public bool GetStrikeout()
        {
            return strikeout;
        }

        public string GetLink()
        {
            return link;
        }

        public int GetWidth()
        {
            return bounds.Width;
        }

        public int GetHeight()
        {
            return bounds.Height;
        }

        public void SetTextBounds(Rectangle bounds)
        {
            if (img == null) //only applies to text chunks
                this.bounds = bounds;
        }

        public string IfMouseOverReturnLink(Point mousePos)
        {
            if (link != "" && bounds.Contains(mousePos))
            {
                mouseOver = true;
                return link;
            }
            else
            {
                mouseOver = false;
                return "";
            }
        }

        public void MouseNotOver()
        {
            mouseOver = false;
        }

        public void Draw(Graphics gfx, Font font, ref int x, ref int y)
        {
            Draw(gfx, font, ref x, ref y, text.Length);
        }

        public void Draw(Graphics gfx, Font font, ref int x, ref int y, int lastChar)
        {
            bounds.X = x;
            bounds.Y = y;

            if (mouseOver)
                gfx.DrawRectangle(new Pen(brush), bounds);

            if (img == null)
            {
                //print text
                lastChar = Math.Min(text.Length, lastChar);

                if (strikeout)
                    font = new Font(font, FontStyle.Strikeout);

                gfx.DrawString(text.Substring(0, lastChar), font, brush, x, y);
            }
            else
                //draw image 
                gfx.DrawImage(img, x, y, img.Width, img.Height);

            //advance drawing position
            if (newline)
            {
                x = 0;
                y += bounds.Height;
            }
            else
                x += bounds.Width;
        }

        public static List<Chunk> Chunkify(string text, string link, Color color, bool strikeout, int leftMargin, int maxWidth, Font font, bool newline, bool absNewline, Func<string, int> MeasureWidth, int lineH)
        {
            List<Chunk> chunks = new List<Chunk>();

            if (text.Length == 0)
                return chunks;

            while (text != "")
            {
                int len = text.Length;
                int segmentW = MeasureWidth(text.Substring(0, len));

                while (len > 1 && leftMargin + segmentW > maxWidth)
                {
                    if (text.LastIndexOf(' ', len - 1) < 1)
                    {
                        //no more spaces; break a word in two to split the line
                        len--;
                        segmentW = MeasureWidth(text.Substring(0, len));

                        while (len > 1 && leftMargin + segmentW > maxWidth)
                        {
                            len--;
                            segmentW = MeasureWidth(text.Substring(0, len));
                        }

                        break;
                    }
                    else
                    {
                        len = text.LastIndexOf(' ', len - 1);
                        segmentW = MeasureWidth(text.Substring(0, len));
                    }
                }

                chunks.Add(new Chunk(text.Substring(0, len), link, color, strikeout, absNewline || text.Length != len, absNewline && text.Length == len, segmentW, lineH));
                text = text.Substring(len);

                if (len < text.Length)
                    leftMargin = 0;
            }

            return chunks;
        }

        public bool JoinChunk(Chunk nextChunk, int leftMargin, int maxWidth, Font font, Func<string, int> MeasureWidth)
        {
            int newWidth = MeasureWidth(text + nextChunk.text);

            if (!absNewline && leftMargin + newWidth <= maxWidth && brush.Color == nextChunk.brush.Color && strikeout == nextChunk.strikeout)
            {
                text += nextChunk.text;
                bounds.Width = newWidth;
                newline = nextChunk.newline;
                absNewline = nextChunk.absNewline;

                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Function to download Image from website
        /// </summary>
        /// <param name="_URL">URL address to download image</param>
        /// <returns>Image</returns>
        Image downloadImage(string _URL)
        {
            Image _tmpImage = null;

            try
            {
                // Open a connection
                System.Net.HttpWebRequest _HttpWebRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(_URL);

                _HttpWebRequest.AllowWriteStreamBuffering = true;

                // You can also specify additional header values like the user agent or the referer: (Optional)
                _HttpWebRequest.UserAgent = "c#";
                _HttpWebRequest.Referer = "http://www.google.com/";

                // set timeout for 20 seconds (Optional)
                _HttpWebRequest.Timeout = 20000;

                // Request response:
                System.Net.WebResponse _WebResponse = _HttpWebRequest.GetResponse();

                // Open data stream:
                System.IO.Stream _WebStream = _WebResponse.GetResponseStream();

                // convert webstream to image
                _tmpImage = Image.FromStream(_WebStream);

                // Cleanup
                _WebResponse.Close();
                _WebResponse.Close();
            }
            catch (Exception _Exception)
            {
                // Error
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
                return null;
            }

            return _tmpImage;
        }
    }
}
