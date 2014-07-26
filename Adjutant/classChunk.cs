using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.ComponentModel;

namespace Adjutant
{
    class Chunk
    {
        public static Func<string, int> MeasureWidth; //link to MeasureWidth() in formMain
        public static Action UpdateImages; //used to call update() in formMain.cs after image changes
        public static EventHandler OnFrameChangedEvent; //used for animated gifs
        public static Font Font; //current console font
        public static Color ErrorColor; //error output color
        public static bool InstantOutput; //if true then imgs don't have an output animation
        public static int PrintAtOnce; //how many chars does the console print together
        public static int LineH; //line height using the current console font
        public static int ConsoleWidth; //used to check if image chunks need to be resized to fit into console
        int prevConsoleWidth; //used to detect when console has resized

        BackgroundWorker imgDownloader;
        bool newImg;

        string text, link;
        bool newline, absNewline, mouseOver, strikeout;
        Rectangle bounds;
        SolidBrush brush;
        Image img;
        int imgW, imgH; //used for image output animation


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
            try
            {
                if (File.Exists(imgURL))
                {
                    img = Image.FromFile(imgURL);

                    if (InstantOutput)
                    {
                        imgW = bounds.Width;
                        imgH = bounds.Height;
                    }
                    else
                    {
                        imgW = Math.Min(PrintAtOnce * MeasureWidth("A"), img.Width);
                        imgH = Math.Min(LineH, img.Height);
                    }
                }
                else
                {
                    //display loading animation while downloading image
                    string loadingGIF = System.Windows.Forms.Application.StartupPath + "\\ui\\loading.gif";

                    if (File.Exists(loadingGIF))
                        img = Image.FromFile(loadingGIF);

                    //prepare worker to dl image
                    imgDownloader = new BackgroundWorker();
                    imgDownloader.DoWork += new DoWorkEventHandler(imgDownloader_DoWork);
                    imgDownloader.RunWorkerCompleted += new RunWorkerCompletedEventHandler(imgDownloader_RunWorkerCompleted);

                    imgDownloader.RunWorkerAsync(imgURL);
                }

                animateGIF();

                this.link = link;
                this.newline = newline;
                this.absNewline = absNewline;

                bounds = new Rectangle(0, 0, img.Width, img.Height);

                text = "";
                strikeout = false;
                brush = new SolidBrush(Color.White);
            }
            catch (Exception exc)
            {
                //convert chunk into text chunk with the error message as text
                text = "Error while displaying image: " + exc.Message;
                brush = new SolidBrush(ErrorColor);
                img = null;
            }
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
            if (IsImgExpanding())
                return imgW;
            else
                return bounds.Width;
        }

        public int GetHeight()
        {
            if (IsImgExpanding())
                return imgH;
            else
                return bounds.Height;
        }

        public bool IsImgExpanding()
        {
            return img != null && (imgW < bounds.Width || imgH < bounds.Height);
        }

        public void DisposeResources()
        {
            if (img != null)
            {
                if (img.GetFrameCount(new FrameDimension(img.FrameDimensionsList[0])) > 1)
                    ImageAnimator.StopAnimate(img, OnFrameChangedEvent);

                img.Dispose();
            }

            if (brush != null)
                brush.Dispose();
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
                //check if console width changed or the image changed
                if (ConsoleWidth != prevConsoleWidth || newImg)
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
                    newImg = false;
                }

                //draw image
                Rectangle destRect = new Rectangle(x, y, imgW, imgH);
                Rectangle srcRect = new Rectangle(0, 0, (int)((float)imgW / bounds.Width * img.Width), (int)((float)imgH / bounds.Height * img.Height));

                gfx.DrawImage(img, destRect, srcRect, GraphicsUnit.Pixel);

                if (IsImgExpanding())
                {
                    if (imgW < bounds.Width)
                        imgW = Math.Min(imgW + PrintAtOnce * MeasureWidth("A"), bounds.Width); //img first grows horizontally to a full-width line
                    else if (imgH < bounds.Height)
                        imgH = Math.Min(imgH + LineH, bounds.Height); //then it grows vertically

                    UpdateImages();
                }
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

        public static List<Chunk> Chunkify(string text, string link, Color color, bool strikeout, int leftMargin, bool newline, bool absNewline)
        {
            List<Chunk> chunks = new List<Chunk>();

            if (text.Length == 0)
                return chunks;

            while (text != "")
            {
                int len = text.Length;
                int segmentW = MeasureWidth(text.Substring(0, len));

                while (len > 1 && leftMargin + segmentW > ConsoleWidth)
                {
                    if (text.LastIndexOf(' ', len - 1) < 1)
                    {
                        //no more spaces; break a word in two to split the line
                        len--;
                        segmentW = MeasureWidth(text.Substring(0, len));

                        while (len > 1 && leftMargin + segmentW > ConsoleWidth)
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

                chunks.Add(new Chunk(text.Substring(0, len), link, color, strikeout, absNewline || text.Length != len, absNewline && text.Length == len, segmentW, LineH));
                text = text.Substring(len);

                if (len < text.Length)
                    leftMargin = 0;
            }

            return chunks;
        }

        public bool JoinChunk(Chunk nextChunk, int leftMargin)
        {
            int newWidth = MeasureWidth(text + nextChunk.text);

            if (!absNewline && leftMargin + newWidth <= ConsoleWidth && brush.Color == nextChunk.brush.Color && strikeout == nextChunk.strikeout)
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

        void imgDownloader_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                e.Result = downloadImage((string)e.Argument);
            }
            catch (Exception exc)
            {
                e.Result = exc.Message;
            }
        }

        void imgDownloader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Image)
            {
                //success
                img = (Image)e.Result;

                bounds.Width = img.Width;
                bounds.Height = img.Height;

                if (InstantOutput)
                {
                    imgW = bounds.Width;
                    imgH = bounds.Height;
                }

                animateGIF();

                newImg = true;
            }
            else
            {
                //convert chunk into text chunk with the error message as text
                text = "Error while downloading image: " + (string)e.Result;
                brush = new SolidBrush(ErrorColor);
                img = null;
            }

            UpdateImages();
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
            }
            catch
            {
                return null;
            }

            return img;
        }

        void animateGIF()
        {
            if (img.GetFrameCount(new FrameDimension(img.FrameDimensionsList[0])) > 1)
                ImageAnimator.Animate(img, OnFrameChangedEvent);
        }
    }
}
