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
        public static int ConsoleWidth; //used to check if image chunks need to be resized to fit into console
        int prevConsoleWidth; //used to detect when console has resized

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

        public void Draw(Graphics gfx, Font font, ref int x, ref int y, ref int h)
        {
            Draw(gfx, font, ref x, ref y, ref h, text.Length);
        }

        public void Draw(Graphics gfx, Font font, ref int x, ref int y, ref int h, int lastChar)
        {
            bounds.X = x;
            bounds.Y = y;
            
            if (bounds.Height > h)
                h = bounds.Height;

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
            {
                //check if console width changed
                if (ConsoleWidth != prevConsoleWidth)
                {
                    bounds.Width = img.Width;
                    bounds.Height = img.Height;

                    if (x + bounds.Width > ConsoleWidth)
                    {
                        //image needs to be resized
                        bounds.Width = ConsoleWidth - x;
                        bounds.Height = (int)((float)bounds.Width / img.Width * img.Height);
                    }

                    prevConsoleWidth = ConsoleWidth;
                }

                //draw image
                gfx.DrawImage(img, x, y, bounds.Width, bounds.Height);
            }

            //advance drawing position
            if (newline)
            {
                x = 0;
                y += h;

                h = 0;
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

        Image downloadImage(string url)
        {
            Image img = null;

            try
            {
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(url);

                request.AllowWriteStreamBuffering = true;

                request.UserAgent = "c#";
                request.Referer = "http://www.google.com/";
                request.Timeout = 20000;

                System.Net.WebResponse response = request.GetResponse();
                System.IO.Stream stream = response.GetResponseStream();
                img = Image.FromStream(stream);

                response.Close();
                response.Close();
            }
            catch
            {
                return null;
            }

            return img;
        }
    }
}
