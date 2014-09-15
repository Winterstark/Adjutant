using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace Adjutant
{
    class Chunk
    {
        public static Func<string, int> MeasureWidth; //link to MeasureWidth() in formMain
        public static Action ForceConsoleResizeEvent; //used to check if error text fits into console
        public static Action<Chunk> UpdateImage; //used to call update() in formMain.cs after image changes
        public static Action<Chunk> SendChunkToNewLine; //used when an image grows too big for its current line
        public static Action<Chunk, int> CheckIfBully; //used to check if an image grew so big it pushed another chunk out of console bounds
        public static List<Chunk> ExpandingChunks; //list of chunks that have growing images
        public static EventHandler OnFrameChangedEvent; //used for animated gifs
        public static Font Font; //current console font
        public static Color ErrorColor; //error output color
        public static Pen WavePen, WavePen2; //pens used to draw waves around the spinner
        public static Image Spinner; //progress indicator when downloading image
        public static bool InstantOutput; //if true then imgs don't have an output animation
        public static int PrintAtOnce; //how many chars does the console print together
        public static int LineH; //line height using the current console font
        public static int ConsoleWidth; //used to check if image chunks need to be resized to fit into console

        float dlProgress; //progress downloading image
        int prevConsoleWidth; //used to detect when console has resized
        int imgW, imgH; //used for image output animation
        int prevX; //previous x value used to draw this chunk (used to detect when x changes (or a new image has been downloaded) to see if it'll fit into the console without resizing)

        string text, link;
        bool newline; //flexible newline - if the console window grows in width this chunk can be joined with the next chunk into a single line
        bool absNewline; //absolute newline - indicates this chunk is always the last chunk in the line
        bool defNewline;  //definitive newline - the next line will start at x=0, regardless of any large image chunks that precede this chunk
        bool xNewline;
        bool mouseOver, strikeout;
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
            dlProgress = -1;
            prevX = -1;
        }

        public Chunk(string imgURL, string link, bool newline, bool absNewline)
        {
            try
            {
                if (Uri.IsWellFormedUriString(imgURL, UriKind.Absolute))
                {
                    //prepare img download
                    imgW = Spinner.Width + 20;
                    imgH = Spinner.Height + 20;
                    bounds = new Rectangle(0, 0, imgW, imgH);

                    WebClient client = new WebClient();

                    client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
                    client.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadDataCompleted);

                    dlProgress = 0;
                    client.DownloadDataAsync(new Uri(imgURL));
                }
                else if (File.Exists(imgURL))
                {
                    img = Image.FromFile(imgURL);
                    checkImgWidth();

                    if (InstantOutput)
                    {
                        imgW = bounds.Width;
                        imgH = bounds.Height;
                    }
                    else
                    {
                        imgW = Math.Min(PrintAtOnce * MeasureWidth("A"), img.Width);
                        imgH = Math.Min(LineH, img.Height);
                        ExpandingChunks.Add(this);
                    }

                    animateGIF();
                    dlProgress = -1;
                }
                else
                {
                    text = "Error while displaying image. Image URL is not valid.";
                    brush = new SolidBrush(ErrorColor);
                    prevX = -1;
                    dlProgress = -1;
                    ForceConsoleResizeEvent();

                    return;
                }

                this.link = link;
                this.newline = newline;
                this.absNewline = absNewline;

                text = "";
                strikeout = false;
                brush = new SolidBrush(Color.White);
                prevX = -1;
            }
            catch (Exception exc)
            {
                //convert chunk into text chunk with the error message as text
                text = "Error while displaying image: " + exc.Message;
                brush = new SolidBrush(ErrorColor);
                img = null;
                prevX = -1;
                dlProgress = -1;
                ForceConsoleResizeEvent();
            }
        }

        public Chunk(Image img, string link)
        {
            this.img = img;
            this.link = link;
            text = "";

            checkImgWidth();
            imgW = bounds.Width;
            imgH = bounds.Height;
            dlProgress = -1;
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

        public void InsertDefinitiveNewline()
        {
            newline = true;
            absNewline = true;
            defNewline = true;
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

        public int GetWidth(bool maxImageWidth)
        {
            if (maxImageWidth && img != null)
                return img.Width;
            else
                return GetWidth();
        }

        public int GetHeight()
        {
            if (IsImgExpanding())
                return imgH;
            else
                return bounds.Height;
        }

        void checkImgWidth()
        {
            bounds.Width = img.Width;
            bounds.Height = img.Height;

            //check if image too big for its current line
            if (prevX + bounds.Width > ConsoleWidth)
            {
                if (prevX > 0)
                    //send image to new line
                    SendChunkToNewLine(this);
                else
                {
                    //image needs to be resized
                    bounds.Width = ConsoleWidth - prevX;
                    bounds.Height = (int)((float)bounds.Width / img.Width * img.Height);
                }
            }

            //check if there is another chunk in this line that doesn't fit anymore
            if (!newline)
                CheckIfBully(this, prevX + bounds.Width);
        }

        public bool IsImgExpanding()
        {
            return img != null && (imgW < bounds.Width || imgH < bounds.Height);
        }

        public void ExpandImage()
        {
            if (imgW < bounds.Width)
                imgW = Math.Min(imgW + PrintAtOnce * MeasureWidth("A"), bounds.Width); //img first grows horizontally to a full-width line
            else if (imgH < bounds.Height)
                imgH = Math.Min(imgH + LineH, bounds.Height); //then it grows vertically
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

        public bool MouseNotOver(string link)
        {
            return mouseOver = this.link == link && link != "";
        }

        public void Draw(Graphics gfx, Font font, ref int x, ref int y, ref int h, ref int prevChunkRight, ref int prevChunkH)
        {
            Draw(gfx, font, ref x, ref y, ref h, ref prevChunkH, ref prevChunkRight, text.Length);
        }

        public void Draw(Graphics gfx, Font font, ref int x, ref int y, ref int h, ref int prevChunkRight, ref int prevChunkH, int lastChar)
        {
            bounds.X = x;
            bounds.Y = y;

            if (bounds.Height > h)
                h = bounds.Height;

            if (dlProgress != -1)
            {
                //draw progress gif
                if (Spinner != null)
                    gfx.DrawImage(Spinner, x + 10, y + 10);

                //draw wavy lines around the gif
                double angle = -Math.PI / 2, angleWave = 0;
                double endAngle = angle + dlProgress * 2 * Math.PI;
                double radX = (Spinner.Width + 14) / 2, radY = (Spinner.Height + 12) / 2, radWave = 2;
                double centerX = 3 + x + radX, centerY = 4 + y + radY;

                double wave = Math.Sin(angleWave) * radWave;
                double prevX = centerX + (radX + wave) * Math.Cos(angle), prevY = centerY + (radY + wave) * Math.Sin(angle), nextX, nextY;

                double phase = Math.PI;
                double wave2 = Math.Sin(angleWave + phase) * radWave;
                double prevX2 = centerX + (radX + wave2) * Math.Cos(angle), prevY2 = centerY + (radY + wave) * Math.Sin(angle), nextX2, nextY2;

                while (angle <= endAngle)
                {
                    angle += 0.01;
                    angleWave += 0.1;

                    wave = Math.Sin(angleWave) * radWave;
                    wave2 = Math.Sin(angleWave + phase) * radWave;

                    nextX = centerX + (radX + wave) * Math.Cos(angle);
                    nextY = centerY + (radY + wave) * Math.Sin(angle);
                    nextX2 = centerX + (radX + wave2) * Math.Cos(angle);
                    nextY2 = centerY + (radY + wave2) * Math.Sin(angle);

                    gfx.DrawLine(WavePen2, (int)prevX2, (int)prevY2, (int)nextX2, (int)nextY2);
                    gfx.DrawLine(WavePen, (int)prevX, (int)prevY, (int)nextX, (int)nextY);

                    prevX = nextX;
                    prevY = nextY;
                    prevX2 = nextX2;
                    prevY2 = nextY2;
                }
            }
            else if (img != null)
            {
                //draw image
                //check if console width or x-coordinate changed
                if (ConsoleWidth != prevConsoleWidth || x != prevX)
                {
                    prevConsoleWidth = ConsoleWidth;
                    prevX = x;

                    checkImgWidth();
                }

                //draw image
                Rectangle destRect = new Rectangle(x, y, imgW, imgH);
                Rectangle srcRect = new Rectangle(0, 0, (int)((float)imgW / bounds.Width * img.Width), (int)((float)imgH / bounds.Height * img.Height));

                gfx.DrawImage(img, destRect, srcRect, GraphicsUnit.Pixel);
            }
            else
            {
                //print text
                lastChar = Math.Min(text.Length, lastChar);

                if (strikeout)
                    font = new Font(font, FontStyle.Strikeout);

                gfx.DrawString(text.Substring(0, lastChar), font, brush, x, y);
            }

            //draw selection rectangle (if mouse over)
            if (mouseOver)
                gfx.DrawRectangle(new Pen(brush), bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            //advance drawing position
            if (newline)
            {
                if (prevChunkH > 2 * bounds.Height && !defNewline)
                {
                    //the previous chunk was a large image so draw the next line to the right of it
                    x = prevChunkRight;
                    y += bounds.Height;

                    h = prevChunkH - bounds.Height;
                    prevChunkH = h;
                }
                else
                {
                    //proper new line
                    x = 0;
                    y += h;

                    h = 0;
                    prevChunkRight = 0;
                    prevChunkH = 0;
                }
            }
            else
            {
                x += bounds.Width;

                prevChunkRight = x;
                prevChunkH = bounds.Height;
            }
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

        void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            dlProgress = (float)e.ProgressPercentage / 100;
            UpdateImage(this);
        }

        void DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            try
            {
                ImageConverter imgConv = new ImageConverter();
                img = (Image)imgConv.ConvertFrom(e.Result);

                bounds.Width = img.Width;
                bounds.Height = img.Height;

                checkImgWidth();

                if (InstantOutput)
                {
                    imgW = bounds.Width;
                    imgH = bounds.Height;
                }
                else
                    ExpandingChunks.Add(this);

                animateGIF();
                UpdateImage(this);
            }
            catch (Exception exc)
            {
                //convert chunk into text chunk with the error message as text
                text = "Error while downloading image: " + exc.Message + " Inner exception message: " + exc.InnerException.Message;
                brush = new SolidBrush(ErrorColor);
                ForceConsoleResizeEvent();
                img = null;
            }

            dlProgress = -1;
        }

        void animateGIF()
        {
            if (ImageAnimator.CanAnimate(img))
                ImageAnimator.Animate(img, OnFrameChangedEvent);
        }
    }
}
