using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using ScintillaNET;


namespace Adjutant
{
    public partial class formMain : Form
    {
        #region Declarations
        const string VERSION = "0.7";
        const string YEAR = "2014";
        const bool RUN_WITHOUT_TWITTER_AND_GMAIL = true; //useful when debugging and repeatedly restarting Adjutant

        const int ZERO_DELAY = 60001;
        const int SND_ASYNC = 0x0001;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("WinMM.dll")]
        static extern bool PlaySound(string fname, int Mod, int flag);

        enum InputMode { Default, AwaitingUsername, Tutorial, ProcessRedirect, Twitter, TwitterPIN, Reddit, Launcher };
        enum OutputMode { Default, Selection, Pad };
        enum HideStyle { Disappear, Fade, ScrollUp, ScrollDown, ScrollLeft, ScrollRight };
        InputMode inputMode;
        OutputMode outputMode;
        HideStyle hideStyle;

        Gmail gmail;
        Twitter twitter;
        Reddit reddit;
        Reddit.Post lastPost;

        UserActivityHook actHook;
        formOptions options;
        Process proc;
        BufferedGraphicsContext context;
        BufferedGraphics grafx;
        Brush brush = Brushes.White;
        Color echoColor, errorColor, helpColor, todoMiscColor, todoItemColor, todoDoneColor, mailCountColor, mailHeaderColor, mailSummaryColor, twUserColor, twMiscColor, twTweetColor, twLinkColor, twTimeColor, twCountColor, redditLinkColor, redditMiscColor;
        DateTime autoHide, pauseEnd, lastTwCount;
        List<Chunk> chunks = new List<Chunk>();
        Dictionary<string, string> customCmds;
        List<string> history = new List<string>(), twURLs = new List<string>(), twMentions = new List<string>(), todo;
        List<Chunk> expandingChunks = new List<Chunk>();
        string[] filteredPaths;
        string user, dir, todoDir, token, secret, link, mailUser, mailPass, twUsername, twSound, mailSound;
        double opacityPassive, opacityActive;
        long lastTweet;
        int x, y, lineH, minH, maxH, prevH, prevX, prevY, leftMargin, yOffset, chunkOffset, lastChunk, lastChunkChar, printAtOnce, autoHideDelay, tabInd, historyInd, minTweetPeriod, twSoundThreshold, hotkey, newMailCount, prevNewMailCount, mailSoundThreshold, tutorialStep;
        bool initialized, winKey, prompt, blankLine, echo, ctrlKey, drag, resizeW, resizeH, autoResize, hiding, hidden, todoHideDone, todoAutoTransfer, twUpdateOnNewTweet, twUpdateOnFocus, twPrevCountBelowThreshold, twDisplayPictures, twDisplayInstagrams, mailUpdateOnNewMail, mailUpdateOnFocus, hotkeyCtrl, hotkeyAlt, hotkeyShift;

        //Pad mode fields
        Dictionary<string, string> padLanguages; //list of interpreters or compilers used for various languages
        DateTime programStarted;
        string padFilePath, padFileContents;
        int padW, padH; //console size when in Pad mode
        bool padResizingW, padResizingH;

        //Launcher fields
        List<Chunk> launcherChunks = new List<Chunk>();
        List<string> launcherIndex = new List<string>();
        string launcherScanDirs, launcherPrevSearch;
        int launcherHotkey, launcherMaxSuggestions, launcherScanPeriod, launcherSelection;
        bool launcherAutohide, launcherHotkeyCtrl, launcherHotkeyAlt, launcherHotkeyShift;

        //Weather fields
        bool weatherMetric, weatherShowOnStart;
        string weatherLocation, weatherLang, weatherWebcam;

        int consoleW, consoleH; //console size to return to
        #endregion


        void initGrafx()
        {
            int maxWindowW = this.Width, maxWindowH = this.Height;

            if (maxH > maxWindowH)
                maxWindowH = maxH;

            context.MaximumBuffer = new Size(maxWindowW + 1, maxWindowH + 1);

            grafx = context.Allocate(this.CreateGraphics(), new Rectangle(0, 0, maxWindowW, maxWindowH));
            grafx.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        }

        void activateConsole() //or hide if already active
        {   
            if (hideStyle == HideStyle.Disappear)
            {
                this.Visible = !this.Visible;
                hidden = !this.Visible;
            }
            else
            {
                if (timerShowHide.Enabled)
                    hiding = !hiding;
                else if (!hidden && !txtCMD.Focused && !txtSelection.Focused && !sciPad.Focused)
                    this.Opacity = opacityActive;
                else
                {
                    if (!hidden)
                        hiding = true;
                    else
                    {
                        this.Visible = true;
                        hiding = false;
                    }

                    timerShowHide.Enabled = true;
                }
            }

            if (this.Visible && !hiding)
                SetForegroundWindow(this.Handle);
        }

        void setOutputMode(OutputMode newOutputMode)
        {
            switch (newOutputMode)
            {
                case OutputMode.Default:
                    txtSelection.Visible = false;
                    sciPad.Visible = false;
                    padMenus.Visible = false;

                    lblPrompt.Visible = true;
                    txtCMD.Visible = true;
                    txtCMD.Focus();

                    if (outputMode == OutputMode.Pad)
                        timerResizeWindow.Enabled = true;
                    break;
                case OutputMode.Selection:
                    if (chunks.Count > 0 && lastChunk != -1)
                    {
                        //get console output
                        string consoleOutput = "";
                        int ind = -1; //where console output begins on screen
                        int h = 0; //line max height
                        Dictionary<int, int> extraLines = new Dictionary<int, int>(); //marks how many newline chars are inserted and at what positions

                        for (int i = 0; i <= lastChunk && i < chunks.Count; i++)
                        {
                            if (chunkOffset == i)
                                ind = consoleOutput.Length;

                            consoleOutput += chunks[i].GetText();

                            //remember line max height
                            if (chunks[i].GetHeight() > h)
                                h = chunks[i].GetHeight();

                            if (chunks[i].IsNewline())
                            {
                                h -= lineH;

                                //approximate image space with blank lines
                                while (h > lineH / 2)
                                {
                                    consoleOutput += Environment.NewLine;
                                    h -= lineH;

                                    //remember how many extra chars are added at this position
                                    int pos = consoleOutput.Length;

                                    if (!extraLines.ContainsKey(pos))
                                        extraLines.Add(pos, Environment.NewLine.Length);
                                    else
                                        extraLines[pos] += Environment.NewLine.Length;
                                }

                                h = 0;
                            }
                        }

                        //display in txtbox
                        txtSelection.SelectionStart = 0;
                        txtSelection.Text = consoleOutput;

                        txtSelection.Width = this.Width;
                        txtSelection.Height = txtCMD.Top;
                        txtSelection.Visible = true;

                        sciPad.Visible = false;

                        //scroll to current position
                        if (ind == -1)
                            ind = consoleOutput.Length;

                        //scroll to end
                        txtSelection.Select(txtSelection.Text.Length, 0);
                        txtSelection.ScrollToCaret();

                        //take into account extra blank lines inserted to approximate image chunks space
                        int extraChars = 0;

                        foreach (var extraLine in extraLines)
                            if (extraLine.Key <= ind)
                                extraChars += extraLine.Value;
                            else
                                break;

                        //take into account yOffset
                        while (yOffset < lineH / 2)
                        {
                            extraChars += Environment.NewLine.Length;
                            yOffset += lineH;
                        }

                        ind += extraChars;

                        //then scroll back where console output begins on screen
                        txtSelection.Select(ind, 0);
                        txtSelection.ScrollToCaret();

                        txtSelection.Focus();
                    }
                    break;
                case OutputMode.Pad:
                    sciPad.Width = this.Width;
                    sciPad.Height = this.Height - padMenus.Height;

                    sciPad.Visible = true;
                    sciPad.Focus();

                    txtCMD.Visible = false;
                    txtSelection.Visible = false;
                    lblPrompt.Visible = false;
                    padMenus.Visible = true;

                    padNewFile();

                    //resize window
                    consoleW = this.Width;
                    consoleH = this.Height;
                    timerResizeWindow.Enabled = true;
                    break;
            }

            outputMode = newOutputMode;
        }

        void draw(Graphics gfx)
        {
            gfx.Clear(this.BackColor);

            if (inputMode == InputMode.Launcher)
            {
                if (launcherChunks.Count > 0)
                {
                    //set starting chunk so that the selected item will be displayed
                    int chunkH = launcherChunks[0].GetHeight();
                    int nMaxChunks = txtCMD.Top / chunkH;
                    int chInd = 2 * Math.Max(launcherSelection - nMaxChunks + 1, 0);
                    int prevChunkRight = 0, prevChunkH = 0;

                    //draw launcher chunks
                    int x = 0, y = yOffset, h = 0;

                    while (chInd < launcherChunks.Count && y < txtCMD.Top)
                    {
                        if (chInd % 2 == 0 && chInd / 2 == launcherSelection) //draw selection rectangle
                            gfx.DrawRectangle(new Pen(txtCMD.ForeColor, 1), x, y, this.Width - 1, launcherChunks[chInd].GetHeight());

                        launcherChunks[chInd++].Draw(gfx, txtCMD.Font, ref x, ref y, ref h, ref prevChunkRight, ref prevChunkH);
                    }
                }
            }
            else
                if (chunks.Count > 0)
                {
                    int x = 0, y = yOffset, h = 0;
                    int chInd = chunkOffset;
                    int prevChunkRight = 0, prevChunkH = 0;

                    while (chInd < lastChunk && y < txtCMD.Top)
                        chunks[chInd++].Draw(gfx, txtCMD.Font, ref x, ref y, ref h, ref prevChunkRight, ref prevChunkH);
                    chunks[lastChunk].Draw(gfx, txtCMD.Font, ref x, ref y, ref h, ref prevChunkRight, ref prevChunkH, lastChunkChar);

                    //expand image chunks
                    for (int i = 0; i < expandingChunks.Count; i++)
                        //expand only chunks that have been printed
                        if (getChunkIndex(expandingChunks[i]) <= lastChunk)
                        {
                            expandingChunks[i].ExpandImage();
                            updateImage(expandingChunks[i]);

                            if (!expandingChunks[i].IsImgExpanding())
                                expandingChunks.RemoveAt(i--);
                        }
                }
        }

        int measureWidth(string txt)
        {
            return (int)grafx.Graphics.MeasureString(txt, txtCMD.Font, int.MaxValue, new StringFormat(StringFormatFlags.MeasureTrailingSpaces)).Width;
            //return (int)Math.Ceiling(grafx.Graphics.MeasureString(txt, txtCMD.Font, int.MaxValue, new StringFormat(StringFormatFlags.MeasureTrailingSpaces)).Width);
        }

        string getWindowText(IntPtr handle)
        {
            StringBuilder buff = new StringBuilder(256);

            if (GetWindowText(handle, buff, 256) > 0)
                return buff.ToString();
            else
                return string.Empty;
        }

        void loadOptions()
        {
            StreamReader file = new StreamReader("options.txt");
            string line;
            string[] args;
            string fontFamily = "Arial";
            int fontSize = 10;
            bool bold = false, italic = false;

            while (!file.EndOfStream)
            {
                line = file.ReadLine();

                if (line != "" && line.Substring(0, 2) != "//")
                {
                    args = line.Split('=');

                    switch (args[0])
                    {
                        case "x":
                            x = int.Parse(args[1]);
                            this.Left = x;
                            break;
                        case "y":
                            y = int.Parse(args[1]);
                            this.Top = y;
                            break;
                        case "width":
                            this.Width = Math.Max(int.Parse(args[1]), 50);
                            break;
                        case "min_height":
                            minH = Math.Max(int.Parse(args[1]), 50);
                            this.Height = minH;
                            break;
                        case "max_height":
                            maxH = Math.Max(int.Parse(args[1]), 50);
                            break;
                        case "global_hotkey":
                            Hotkey.StringToHotkey(args[1], out hotkey, out hotkeyCtrl, out hotkeyAlt, out hotkeyShift);
                            Hotkey.RegisterHotKey(this, hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift, false);
                            break;
                        case "hide_delay":
                            autoHideDelay = int.Parse(args[1]);
                            break;
                        case "hide_style":
                            hideStyle = (HideStyle)Enum.Parse(typeof(HideStyle), args[1]);
                            break;
                        case "opacity_passive":
                            opacityPassive = double.Parse(args[1]);
                            break;
                        case "opacity_active":
                            opacityActive = double.Parse(args[1]);
                            break;
                        case "background_color":
                            txtCMD.BackColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "text_color":
                            txtCMD.ForeColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "text_font":
                            fontFamily = args[1];
                            break;
                        case "text_size":
                            fontSize = int.Parse(args[1]);
                            break;
                        case "text_bold":
                            bold = bool.Parse(args[1]);
                            break;
                        case "text_italic":
                            italic = bool.Parse(args[1]);
                            break;
                        case "print_delay":
                            setDelay(timerPrint, int.Parse(args[1]));
                            break;
                        case "print_atonce":
                            printAtOnce = int.Parse(args[1]);
                            printAtOnce = Math.Min(Math.Max(printAtOnce, 1), 50);
                            break;
                        case "start_dir":
                            dir = args[1];
                            break;
                        case "prompt":
                            prompt = bool.Parse(args[1]);
                            break;
                        case "blank_line":
                            blankLine = bool.Parse(args[1]);
                            break;
                        case "echo":
                            echo = bool.Parse(args[1]);
                            break;
                        case "echo_color":
                            echoColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "error_color":
                            errorColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "todo_dir":
                            todoDir = args[1];
                            if (todoDir.Length > 0 && todoDir[todoDir.Length - 1] != '\\')
                                todoDir += "\\";
                            break;
                        case "todo_hide_done":
                            todoHideDone = bool.Parse(args[1]);
                            break;
                        case "todo_auto_transfer":
                            todoAutoTransfer = bool.Parse(args[1]);
                            break;
                        case "todo_misc_color":
                            todoMiscColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "todo_item_color":
                            todoItemColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "todo_done_color":
                            todoDoneColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "token":
                            token = args[1];
                            break;
                        case "secret":
                            secret = args[1];
                            break;
                        case "last_tweet":
                            lastTweet = long.Parse(args[1]);
                            break;
                        case "update_on_new_tweet":
                            twUpdateOnNewTweet = bool.Parse(args[1]);
                            break;
                        case "update_on_focus":
                            twUpdateOnFocus = bool.Parse(args[1]);
                            break;
                        case "min_tweet_period":
                            minTweetPeriod = int.Parse(args[1]);
                            break;
                        case "display_pictures":
                            twDisplayPictures = bool.Parse(args[1]);
                            break;
                        case "display_instagrams":
                            twDisplayInstagrams = bool.Parse(args[1]);
                            break;
                        case "mail_period":
                            timerMailCheck.Interval = int.Parse(args[1]) * 60000; //convert from minutes to miliseconds
                            break;
                        case "mail_update_on_new_mail":
                            mailUpdateOnNewMail = bool.Parse(args[1]);
                            break;
                        case "mail_update_on_focus":
                            mailUpdateOnFocus = bool.Parse(args[1]);
                            break;
                        case "mail_sound":
                            mailSound = args[1];
                            break;
                        case "mail_sound_threshold":
                            mailSoundThreshold = int.Parse(args[1]);
                            break;
                        case "mail_count_color":
                            mailCountColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "mail_header_color":
                            mailHeaderColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "mail_summary_color":
                            mailSummaryColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "tw_sound":
                            twSound = args[1];
                            break;
                        case "tw_sound_threshold":
                            twSoundThreshold = int.Parse(args[1]);
                            break;
                        case "tw_user_color":
                            twUserColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "tw_misc_color":
                            twMiscColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "tw_tweet_color":
                            twTweetColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "tw_link_color":
                            twLinkColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "tw_time_color":
                            twTimeColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "tw_count_color":
                            twCountColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "reddit_link_color":
                            redditLinkColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "reddit_misc_color":
                            redditMiscColor = Color.FromArgb(int.Parse(args[1]));
                            break;
                        case "pad_width":
                            padW = int.Parse(args[1]);
                            break;
                        case "pad_height":
                            padH = int.Parse(args[1]);
                            break;
                        case "pad_word_wrap":
                            padMenusViewWordWrap.Checked = bool.Parse(args[1]);

                            if (padMenusViewWordWrap.Checked)
                                sciPad.LineWrapping.Mode = LineWrappingMode.Word;
                            else
                                sciPad.LineWrapping.Mode = LineWrappingMode.None;
                            break;
                        case "pad_line_numbers":
                            padMenusViewShowLineNumbers.Checked = bool.Parse(args[1]);

                            if (padMenusViewShowLineNumbers.Checked)
                                sciPad.Margins[0].Width = 20;
                            else
                                sciPad.Margins[0].Width = 0;
                            break;
                        case "pad_languages":
                            padLanguages = new Dictionary<string, string>();

                            foreach (string lang in args[1].Split(new string[] { " / " }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kvPair = lang.Split('>');
                                padLanguages.Add(kvPair[0], kvPair[1]);
                            }
                            break;
                        case "launcher_hotkey":
                            Hotkey.StringToHotkey(args[1], out launcherHotkey, out launcherHotkeyCtrl, out launcherHotkeyAlt, out launcherHotkeyShift);
                            Hotkey.RegisterHotKey(this, launcherHotkey, launcherHotkeyCtrl, launcherHotkeyAlt, launcherHotkeyShift, true);
                            break;
                        case "launcher_max_suggestions":
                            launcherMaxSuggestions = int.Parse(args[1]);
                            break;
                        case "launcher_autohide":
                            launcherAutohide = bool.Parse(args[1]);
                            break;
                        case "launcher_scan_period":
                             launcherScanPeriod = int.Parse(args[1]);
                            break;
                        case "launcher_scan_dirs":
                            launcherScanDirs = args[1];
                            break;
                        case "weather_location":
                            weatherLocation = args[1];
                            break;
                        case "weather_metric":
                            weatherMetric = bool.Parse(args[1]);
                            break;
                        case "weather_show_on_start":
                            weatherShowOnStart = bool.Parse(args[1]);
                            break;
                        case "weather_lang":
                            weatherLang = args[1];
                            break;
                        case "weather_webcam":
                            weatherWebcam = args[1];
                            break;
                        case "user":
                            user = args[1];
                            break;
                    }
                }
            }

            file.Close();

            //construct and apply font
            FontStyle fontStyle;

            if (bold && italic)
                fontStyle = FontStyle.Bold | FontStyle.Italic;
            else if (bold)
                fontStyle = FontStyle.Bold;
            else if (italic)
                fontStyle = FontStyle.Italic;
            else
                fontStyle = FontStyle.Regular;

            txtCMD.Font = new Font(fontFamily, fontSize, fontStyle);

            //apply console style to other UI controls
            applyUIStyle();

            setPrompt();

            //load mail username/password
            if (File.Exists(Application.StartupPath + "\\mail_login.dat"))
            {
                AES alg = new AES();
                alg.Decrypt(Application.StartupPath + "\\mail_login.dat", Application.StartupPath + "\\temp.dat");

                StreamReader login = new StreamReader(Application.StartupPath + "\\temp.dat");
                mailUser = login.ReadLine();
                mailPass = login.ReadLine();
                login.Close();

                File.Delete(Application.StartupPath + "\\temp.dat");
            }

            //update static values in Chunk
            Chunk.Font = txtCMD.Font;
            Chunk.PrintAtOnce = printAtOnce;
            Chunk.InstantOutput = timerPrint.Interval == ZERO_DELAY;
        }

        void saveOptions()
        {
            //prepare languages dictionary for writing
            string langs = "";

            if (padLanguages.Count > 0)
            {
                foreach (var lang in padLanguages)
                    langs += lang.Key + ">" + lang.Value + " / ";

                langs = langs.Substring(0, langs.Length - 3);
            }

            //save options
            StreamWriter file = new System.IO.StreamWriter(Application.StartupPath + "\\options.txt");
            
            file.WriteLine("//window");
            file.WriteLine("x=" + x);
            file.WriteLine("y=" + y);
            file.WriteLine("width=" + (outputMode == OutputMode.Pad ? consoleW : this.Width));
            file.WriteLine("min_height=" + minH);
            file.WriteLine("max_height=" + maxH);
            file.WriteLine("global_hotkey=" + Hotkey.HotkeyToString(hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift));
            file.WriteLine("hide_delay=" + autoHideDelay);
            file.WriteLine("hide_style=" + hideStyle);
            file.WriteLine("opacity_passive=" + opacityPassive);
            file.WriteLine("opacity_active=" + opacityActive);
            file.WriteLine("background_color=" + this.BackColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//font");
            file.WriteLine("text_color=" + txtCMD.ForeColor.ToArgb());
            file.WriteLine("text_font=" + txtCMD.Font.Name);
            file.WriteLine("text_size=" + txtCMD.Font.Size);
            file.WriteLine("text_bold=" + txtCMD.Font.Bold);
            file.WriteLine("text_italic=" + txtCMD.Font.Italic);
            file.WriteLine();

            file.WriteLine("//console");
            file.WriteLine("start_dir=" + dir);
            file.WriteLine("print_delay=" + getDelay(timerPrint));
            file.WriteLine("print_atonce=" + printAtOnce);
            file.WriteLine("prompt=" + prompt);
            file.WriteLine("blank_line=" + blankLine);
            file.WriteLine("echo=" + echo);
            file.WriteLine("echo_color=" + echoColor.ToArgb());
            file.WriteLine("error_color=" + errorColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//todo");
            file.WriteLine("todo_dir=" + todoDir);
            file.WriteLine("todo_hide_done=" + todoHideDone);
            file.WriteLine("todo_auto_transfer=" + todoAutoTransfer);
            file.WriteLine("todo_misc_color=" + todoMiscColor.ToArgb());
            file.WriteLine("todo_item_color=" + todoItemColor.ToArgb());
            file.WriteLine("todo_done_color=" + todoDoneColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//mail");
            file.WriteLine("mail_period=" + (timerMailCheck.Interval / 60000)); //convert from miliseconds to minutes
            file.WriteLine("mail_update_on_new_mail=" + mailUpdateOnNewMail);
            file.WriteLine("mail_update_on_focus=" + mailUpdateOnFocus);
            file.WriteLine("mail_sound=" + mailSound);
            file.WriteLine("mail_sound_threshold=" + mailSoundThreshold);
            file.WriteLine("mail_count_color=" + mailCountColor.ToArgb());
            file.WriteLine("mail_header_color=" + mailHeaderColor.ToArgb());
            file.WriteLine("mail_summary_color=" + mailSummaryColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//twitter");
            file.WriteLine("token=" + token);
            file.WriteLine("secret=" + secret);
            file.WriteLine("last_tweet=" + lastTweet);
            file.WriteLine("update_on_new_tweet=" + twUpdateOnNewTweet);
            file.WriteLine("update_on_focus=" + twUpdateOnFocus);
            file.WriteLine("min_tweet_period=" + minTweetPeriod);
            file.WriteLine("display_pictures=" + twDisplayPictures);
            file.WriteLine("display_instagrams=" + twDisplayInstagrams);
            file.WriteLine("tw_sound=" + twSound);
            file.WriteLine("tw_sound_threshold=" + twSoundThreshold);
            file.WriteLine("tw_user_color=" + twUserColor.ToArgb());
            file.WriteLine("tw_misc_color=" + twMiscColor.ToArgb());
            file.WriteLine("tw_tweet_color=" + twTweetColor.ToArgb());
            file.WriteLine("tw_link_color=" + twLinkColor.ToArgb());
            file.WriteLine("tw_time_color=" + twTimeColor.ToArgb());
            file.WriteLine("tw_count_color=" + twCountColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//reddit");
            file.WriteLine("reddit_link_color=" + redditLinkColor.ToArgb());
            file.WriteLine("reddit_misc_color=" + redditMiscColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//pad");
            file.WriteLine("pad_width=" + padW);
            file.WriteLine("pad_height=" + padH);
            file.WriteLine("pad_word_wrap=" + padMenusViewWordWrap.Checked);
            file.WriteLine("pad_line_numbers=" + padMenusViewShowLineNumbers.Checked);
            file.WriteLine("pad_languages=" + langs);
            file.WriteLine();

            file.WriteLine("//launcher");
            file.WriteLine("launcher_hotkey=" + Hotkey.HotkeyToString(launcherHotkey, launcherHotkeyCtrl, launcherHotkeyAlt, launcherHotkeyShift));
            file.WriteLine("launcher_max_suggestions=" + launcherMaxSuggestions);
            file.WriteLine("launcher_autohide=" + launcherAutohide);
            file.WriteLine("launcher_scan_period=" + launcherScanPeriod);
            file.WriteLine("launcher_scan_dirs=" + launcherScanDirs);
            file.WriteLine();

            file.WriteLine("//weather");
            file.WriteLine("weather_location=" + weatherLocation);
            file.WriteLine("weather_metric=" + weatherMetric);
            file.WriteLine("weather_show_on_start=" + weatherShowOnStart);
            file.WriteLine("weather_lang=" + weatherLang);
            file.WriteLine("weather_webcam=" + weatherWebcam);
            file.WriteLine();

            file.WriteLine("//other");
            file.WriteLine("user=" + user);

            file.Close();

            //save mail username/password
            StreamWriter login = new StreamWriter(Application.StartupPath + "\\mail_login.dat");
            login.WriteLine(mailUser);
            login.WriteLine(mailPass);
            login.Close();

            new AES().Encrypt(Application.StartupPath + "\\mail_login.dat", Application.StartupPath + "\\mail_login.dat");
        }

        void showOptions()
        {
            if (options == null || options.IsDisposed)
            {
                options = new formOptions();

                options.Main = this;
                options.Starting = true;

                //load font list
                options.comboFont.Items.Clear();
                foreach (FontFamily font in System.Drawing.FontFamily.Families)
                    options.comboFont.Items.Add(font.Name);

                //build launcher scan dirs dictionary
                options.LauncherScanDirsOriginal = launcherScanDirs;
                options.LauncherScanDirs = new Dictionary<string, string>();
                List<string> dirList = new List<string>();

                foreach (string scanDir in launcherScanDirs.Split('/'))
                    if (scanDir != "")
                    {
                        string[] dirOptions = scanDir.Split('>');
                        options.LauncherScanDirs.Add(dirOptions[0], dirOptions[1]);

                        dirList.Add(dirOptions[0]);
                    }

                //set constraints
                options.numX.Minimum = 0;
                options.numX.Maximum = Screen.PrimaryScreen.WorkingArea.Width;
                options.numY.Minimum = 0;
                options.numY.Maximum = Screen.PrimaryScreen.WorkingArea.Height;
                options.numW.Minimum = 0;
                options.numW.Maximum = Screen.PrimaryScreen.WorkingArea.Width;
                options.numMinH.Minimum = 0;
                options.numMinH.Maximum = Screen.PrimaryScreen.WorkingArea.Height;
                options.numMaxH.Minimum = 0;
                options.numMaxH.Maximum = Screen.PrimaryScreen.WorkingArea.Height;

                //set tags
                options.numX.Tag = x;
                options.numY.Tag = y;
                options.numW.Tag = this.Width;
                options.numMinH.Tag = minH;
                options.numMaxH.Tag = maxH;
                options.txtHotkey.Tag = Hotkey.HotkeyToString(hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift);
                options.numAutoHideDelay.Tag = autoHideDelay;
                options.comboHideStyle.Tag = hideStyle.ToString();
                options.numOpacityActive.Tag = (int)(opacityActive * 100);
                options.numOpacityPassive.Tag = (int)(opacityPassive * 100);
                options.picBackColor.Tag = txtCMD.BackColor;

                options.comboFont.Tag = txtCMD.Font.Name;
                options.numFontSize.Tag = (int)txtCMD.Font.Size;
                options.chkBold.Tag = txtCMD.Font.Bold;
                options.chkItalic.Tag = txtCMD.Font.Italic;
                options.picTextColor.Tag = txtCMD.ForeColor;

                options.txtStartDir.Tag = dir;
                options.numPrintDelay.Tag = getDelay(timerPrint);
                options.numPrintAtOnce.Tag = printAtOnce;
                options.chkPrompt.Tag = prompt;
                options.chkBlankLine.Tag = blankLine;
                options.chkEcho.Tag = echo;
                options.picEchoColor.Tag = echoColor;
                options.picErrorColor.Tag = errorColor;

                options.txtTodoDir.Tag = todoDir;
                options.chkTodoHideDone.Tag = todoHideDone;
                options.chkTodoAutoTransfer.Tag = todoAutoTransfer;
                options.picTodoMiscColor.Tag = todoMiscColor;
                options.picTodoItemColor.Tag = todoItemColor;
                options.picTodoDoneColor.Tag = todoDoneColor;

                options.txtUser.Tag = mailUser;
                options.txtPass.Tag = mailPass;
                options.chkMailCountOnNewMail.Tag = mailUpdateOnNewMail;
                options.chkMailCountOnFocus.Tag = mailUpdateOnFocus;
                options.numMailCheckPeriod.Tag = timerMailCheck.Interval / 60000;
                options.txtMailSound.Tag = mailSound;
                options.numMailSoundThreshold.Tag = mailSoundThreshold;
                options.picMailCountColor.Tag = mailCountColor;
                options.picMailHeaderColor.Tag = mailHeaderColor;
                options.picMailSummaryColor.Tag = mailSummaryColor;

                options.chkTwCountOnNewTweet.Tag = twUpdateOnNewTweet;
                options.chkTwCountOnFocus.Tag = twUpdateOnFocus;
                options.numTwCountMinPeriod.Tag = minTweetPeriod;
                options.chkTwDisplayPictures.Tag = twDisplayPictures;
                options.chkTwDisplayInstagrams.Tag = twDisplayInstagrams;
                options.txtTwSound.Tag = twSound;
                options.numTwSoundThreshold.Tag = twSoundThreshold;
                options.picTwUsernameColor.Tag = twUserColor;
                options.picTwMiscColor.Tag = twMiscColor;
                options.picTwTweetColor.Tag = twTweetColor;
                options.picTwLinkColor.Tag = twLinkColor;
                options.picTwTimestampColor.Tag = twTimeColor;
                options.picTwCountColor.Tag = twCountColor;

                options.picRedditLinkColor.Tag = redditLinkColor;
                options.picRedditMiscColor.Tag = redditMiscColor;

                options.txtLauncherHotkey.Tag = Hotkey.HotkeyToString(launcherHotkey, launcherHotkeyCtrl, launcherHotkeyAlt, launcherHotkeyShift);
                options.numLauncherMaxSuggestions.Tag = launcherMaxSuggestions;
                options.chkLauncherAutohide.Tag = launcherAutohide;
                options.numLauncherScanPeriod.Tag = launcherScanPeriod;

                options.txtWeatherLocation.Tag = weatherLocation;
                options.txtWeatherWebcam.Tag = weatherWebcam;
                options.rdbWeatherMetric.Tag = weatherMetric;
                options.chkWeatherShowOnStart.Tag = weatherShowOnStart;
                options.comboWeatherLang.Tag = weatherLang;

                //set current values
                options.numX.Value = x;
                options.numY.Value = y;
                options.numW.Value = this.Width;
                options.numMinH.Value = minH;
                options.numMaxH.Value = maxH;
                options.txtHotkey.Text = Hotkey.HotkeyToString(hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift);
                options.numAutoHideDelay.Value = autoHideDelay;
                options.comboHideStyle.Text = hideStyle.ToString();
                options.numOpacityActive.Value = (int)(opacityActive * 100);
                options.numOpacityPassive.Value = (int)(opacityPassive * 100);
                options.picBackColor.BackColor = txtCMD.BackColor;

                options.comboFont.Text = txtCMD.Font.Name;
                options.numFontSize.Value = (int)txtCMD.Font.Size;
                options.chkBold.Checked = txtCMD.Font.Bold;
                options.chkItalic.Checked = txtCMD.Font.Italic;
                options.picTextColor.BackColor = txtCMD.ForeColor;

                options.txtStartDir.Text = dir;
                options.numPrintDelay.Value = getDelay(timerPrint);
                options.numPrintAtOnce.Value = printAtOnce;
                options.chkPrompt.Checked = prompt;
                options.chkBlankLine.Checked = blankLine;
                options.chkEcho.Checked = echo;
                options.picEchoColor.BackColor = echoColor;
                options.picErrorColor.BackColor = errorColor;

                options.txtTodoDir.Text = todoDir;
                options.chkTodoHideDone.Checked = todoHideDone;
                options.chkTodoAutoTransfer.Checked = todoAutoTransfer;
                options.picTodoMiscColor.BackColor = todoMiscColor;
                options.picTodoItemColor.BackColor = todoItemColor;
                options.picTodoDoneColor.BackColor = todoDoneColor;

                options.txtUser.Text = mailUser;
                options.txtPass.Text = mailPass;
                options.chkMailCountOnNewMail.Checked = mailUpdateOnNewMail;
                options.chkMailCountOnFocus.Checked = mailUpdateOnFocus;
                options.numMailCheckPeriod.Value = timerMailCheck.Interval / 60000;
                options.txtMailSound.Text = mailSound;
                options.numMailSoundThreshold.Value = mailSoundThreshold;
                options.picMailCountColor.BackColor = mailCountColor;
                options.picMailHeaderColor.BackColor = mailHeaderColor;
                options.picMailSummaryColor.BackColor = mailSummaryColor;

                options.chkTwCountOnNewTweet.Checked = twUpdateOnNewTweet;
                options.chkTwCountOnFocus.Checked = twUpdateOnFocus;
                options.numTwCountMinPeriod.Value = minTweetPeriod;
                options.chkTwDisplayPictures.Checked = twDisplayPictures;
                options.chkTwDisplayInstagrams.Checked = twDisplayInstagrams;
                options.txtTwSound.Text = twSound;
                options.numTwSoundThreshold.Value = twSoundThreshold;
                options.picTwUsernameColor.BackColor = twUserColor;
                options.picTwMiscColor.BackColor = twMiscColor;
                options.picTwTweetColor.BackColor = twTweetColor;
                options.picTwLinkColor.BackColor = twLinkColor;
                options.picTwTimestampColor.BackColor = twTimeColor;
                options.picTwCountColor.BackColor = twCountColor;

                options.picRedditLinkColor.BackColor = redditLinkColor;
                options.picRedditMiscColor.BackColor = redditMiscColor;

                options.txtLauncherHotkey.Text = Hotkey.HotkeyToString(launcherHotkey, launcherHotkeyCtrl, launcherHotkeyAlt, launcherHotkeyShift);
                options.numLauncherMaxSuggestions.Value = launcherMaxSuggestions;
                options.chkLauncherAutohide.Checked = launcherAutohide;
                options.numLauncherScanPeriod.Value = launcherScanPeriod;
                options.lstLauncherDirs.Items.AddRange(dirList.ToArray());

                options.txtWeatherLocation.Text = weatherLocation;
                options.txtWeatherWebcam.Text = weatherWebcam;
                if (weatherMetric)
                    options.rdbWeatherMetric.Checked = true;
                else
                    options.rdbWeatherImperial.Checked = true;
                options.chkWeatherShowOnStart.Checked = weatherShowOnStart;
                for (int i = 0; i < options.comboWeatherLang.Items.Count; i++)
                    if (options.comboWeatherLang.Items[i].ToString().Contains("/" + weatherLang))
                    {
                        options.comboWeatherLang.SelectedIndex = i;
                        break;
                    }

                options.Starting = false;

                options.Show();
            }
        }

        public void UpdateOptions(formOptions options)
        {
            //if todo dir changed copy existing todo lists there
            if (options.txtTodoDir.Text != todoDir)
            {
                string dest = options.txtTodoDir.Text;
                if (dest[dest.Length - 1] != '\\')
                    dest += "\\";

                foreach (string file in Directory.GetFiles(todoDir))
                    File.Copy(file, dest + Path.GetFileName(file));
            }

            int prevHotkey = hotkey, prevLauncherHotkey = launcherHotkey;

            //assign new options
            x = (int)options.numX.Value;
            y = (int)options.numY.Value;
            this.Width = (int)options.numW.Value;
            minH = (int)options.numMinH.Value;
            maxH = (int)options.numMaxH.Value;
            Hotkey.StringToHotkey(options.txtHotkey.Text, out hotkey, out hotkeyCtrl, out hotkeyAlt, out hotkeyShift);
            autoHideDelay = (int)options.numAutoHideDelay.Value;
            hideStyle = (HideStyle)Enum.Parse(typeof(HideStyle), options.comboHideStyle.Text);
            opacityActive = (double)options.numOpacityActive.Value / 100;
            opacityPassive = (double)options.numOpacityPassive.Value / 100;

            txtCMD.BackColor = options.picBackColor.BackColor;

            FontStyle style;
            if (options.chkBold.Checked && options.chkItalic.Checked)
                style = FontStyle.Bold | FontStyle.Italic;
            else if (options.chkBold.Checked)
                style = FontStyle.Bold;
            else if (options.chkItalic.Checked)
                style = FontStyle.Italic;
            else
                style = FontStyle.Regular;

            Font prevFont = lblPrompt.Font;

            txtCMD.Font = new Font(options.comboFont.Text, (float)options.numFontSize.Value, style);
            txtCMD.ForeColor = options.picTextColor.BackColor;

            if (lblPrompt.Font != prevFont)
            {
                this.OnResize(new EventArgs());

                foreach (Chunk chunk in chunks)
                    chunk.SetTextBounds(new Rectangle(0, 0, measureWidth(chunk.ToString()), lineH));

                update();
            }

            applyUIStyle();

            dir = options.txtStartDir.Text;
            setDelay(timerPrint, (int)options.numPrintDelay.Value);
            printAtOnce = (int)options.numPrintAtOnce.Value;
            prompt = options.chkPrompt.Checked;
            blankLine = options.chkBlankLine.Checked;
            echo = options.chkEcho.Checked;
            echoColor = options.picEchoColor.BackColor;
            errorColor = options.picErrorColor.BackColor;

            todoDir = options.txtTodoDir.Text;
            todoHideDone = options.chkTodoHideDone.Checked;
            todoAutoTransfer = options.chkTodoAutoTransfer.Checked;
            todoMiscColor = options.picTodoMiscColor.BackColor;
            todoItemColor = options.picTodoItemColor.BackColor;
            todoDoneColor = options.picTodoDoneColor.BackColor;

            mailUser = options.txtUser.Text;
            mailPass = options.txtPass.Text;
            mailUpdateOnNewMail = options.chkMailCountOnNewMail.Checked;
            mailUpdateOnFocus = options.chkMailCountOnFocus.Checked;
            timerMailCheck.Interval = (int)options.numMailCheckPeriod.Value * 60000;
            mailSound = options.txtMailSound.Text;
            mailSoundThreshold = (int)options.numMailSoundThreshold.Value;
            mailCountColor = options.picMailCountColor.BackColor;
            mailHeaderColor = options.picMailHeaderColor.BackColor;
            mailSummaryColor = options.picMailSummaryColor.BackColor;

            twUpdateOnNewTweet = options.chkTwCountOnNewTweet.Checked;
            twUpdateOnFocus = options.chkTwCountOnFocus.Checked;
            minTweetPeriod = (int)options.numTwCountMinPeriod.Value;
            twDisplayPictures = options.chkTwDisplayPictures.Checked;
            twDisplayInstagrams = options.chkTwDisplayInstagrams.Checked;
            twSound = options.txtTwSound.Text;
            twSoundThreshold = (int)options.numTwSoundThreshold.Value;
            twUserColor = options.picTwUsernameColor.BackColor;
            twMiscColor = options.picTwMiscColor.BackColor;
            twTweetColor = options.picTwTweetColor.BackColor;
            twLinkColor = options.picTwLinkColor.BackColor;
            twTimeColor = options.picTwTimestampColor.BackColor;
            twCountColor = options.picTwCountColor.BackColor;

            redditLinkColor = options.picRedditLinkColor.BackColor;
            redditMiscColor = options.picRedditMiscColor.BackColor;

            Hotkey.StringToHotkey(options.txtLauncherHotkey.Text, out launcherHotkey, out launcherHotkeyCtrl, out launcherHotkeyAlt, out launcherHotkeyShift);
            launcherMaxSuggestions = (int)options.numLauncherMaxSuggestions.Value;
            launcherAutohide = options.chkLauncherAutohide.Checked;
            launcherScanPeriod = (int)options.numLauncherScanPeriod.Value;
            launcherScanDirs = options.GetLauncherScanDirs();

            weatherWebcam = options.txtWeatherWebcam.Text;
            weatherLocation = options.txtWeatherLocation.Text;
            weatherMetric = options.rdbWeatherMetric.Checked;
            weatherShowOnStart = options.chkWeatherShowOnStart.Checked;
            weatherLang = options.comboWeatherLang.Text.Substring(options.comboWeatherLang.Text.IndexOf('/') + 1);

            //apply changes & save
            Chunk.Font = txtCMD.Font;
            Chunk.PrintAtOnce = printAtOnce;
            Chunk.InstantOutput = timerPrint.Interval == ZERO_DELAY;
            Chunk.ErrorColor = errorColor;

            if (hotkey != prevHotkey)
            {
                Hotkey.UnregisterHotKey(this, false);
                Hotkey.RegisterHotKey(this, hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift, false);
            }

            if (launcherHotkey != prevLauncherHotkey)
            {
                Hotkey.UnregisterHotKey(this, true);
                Hotkey.RegisterHotKey(this, launcherHotkey, launcherHotkeyCtrl, launcherHotkeyAlt, launcherHotkeyShift, true);
            }

            Twitter.DisplayPictures = twDisplayPictures;
            Twitter.DisplayInstagrams = twDisplayInstagrams;

            this.Top = y;
            this.Left = x;

            initGrafx();
            windowAutosize();
            setPrompt();

            if (gmail != null)
                gmail.ChangeLogin(mailUser, mailPass);

            saveOptions();
        }

        void applyUIStyle()
        {
            lblPrompt.Font = txtCMD.Font;
            txtSelection.Font = txtCMD.Font;
            sciPad.Font = txtCMD.Font;
            sciPad.Styles.LineNumber.Font = txtCMD.Font;

            lblPrompt.ForeColor = txtCMD.ForeColor;
            txtSelection.ForeColor = txtCMD.ForeColor;
            sciPad.ForeColor = txtCMD.ForeColor;
            sciPad.Caret.Color = txtCMD.ForeColor;
            sciPad.Styles.LineNumber.ForeColor = txtCMD.ForeColor;

            lblPrompt.BackColor = txtCMD.BackColor;
            txtSelection.BackColor = txtCMD.BackColor;
            sciPad.BackColor = txtCMD.BackColor;
            sciPad.Styles.LineNumber.BackColor = txtCMD.BackColor;
            this.BackColor = txtCMD.BackColor;
        }

        void loadCustomCmds()
        {
            customCmds = new Dictionary<string, string>();
            StreamReader file = new StreamReader(Application.StartupPath + "\\custom cmds.txt");

            foreach (string custom in file.ReadToEnd().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = custom.Split('=');

                if (parts.Length == 2)
                    customCmds.Add(parts[0], parts[1]);
            }

            file.Close();
        }

        void saveCustomCmds()
        {
            StreamWriter file = new StreamWriter(Application.StartupPath + "\\custom cmds.txt");
            foreach (var customCmd in customCmds)
                file.WriteLine(customCmd.Key + "=" + customCmd.Value);
            file.Close();
        }

        int getDelay(Timer timer)
        {
            if (timer.Interval == ZERO_DELAY)
                return 0;
            else
                return timer.Interval;
        }

        void setDelay(Timer timer, int delay)
        {
            if (delay == 0)
                timer.Interval = ZERO_DELAY;
            else
                timer.Interval = delay;
        }

        void greeting()
        {
            if (DateTime.Now.Hour >= 6 && DateTime.Now.Hour < 12)
                print("Good morning " + user + ".<pause>");
            else if (DateTime.Now.Hour >= 12 && DateTime.Now.Hour < 19)
                print("Good afternoon " + user + ".<pause>");
            else if (DateTime.Now.Hour >= 19 && DateTime.Now.Hour < 23)
                print("Good evening " + user + ".<pause>");
            else
                print("Staying up late again " + user + "?<pause>");
        }

        void setPrompt()
        {
            string newPrompt;

            switch (inputMode)
            {
                case InputMode.Launcher:
                    newPrompt = "Launch>";
                    break;
                case InputMode.Reddit:
                    newPrompt = "Reddit>";
                    break;
                case InputMode.Twitter:
                    newPrompt = "Twitter>";
                    break;
                case InputMode.TwitterPIN:
                    newPrompt = "Twitter PIN:";
                    break;
                case InputMode.AwaitingUsername:
                    newPrompt = "Username: ";
                    break;
                default:
                    newPrompt = dir + ">";
                    break;
            }

            setPrompt(newPrompt);
        }

        void setPrompt(string newPrompt)
        {
            if (outputMode == OutputMode.Pad)
                return; //disable prompt when running programs from Pad

            if (prompt)
            {
                lblPrompt.Text = newPrompt;

                lblPrompt.Visible = true;
                lblPrompt.Top = txtCMD.Top;
                lblPrompt.Height = txtCMD.Height;
                lblPrompt.Width = measureWidth(lblPrompt.Text + "a"); //for some reason measureWidth's result is 1 character too short (for lblPrompt)
                txtCMD.Left = lblPrompt.Width;
            }
            else
            {
                lblPrompt.Visible = false;
                txtCMD.Left = 0;
            }
        }

        void command()
        {
            //replace custom commands (if the command isn't "custom")
            if (txtCMD.Text.Length < 6 || txtCMD.Text.Substring(0, 6).ToLower() != "custom")
                foreach (var customCmd in customCmds)
                    txtCMD.Text = txtCMD.Text.Replace(customCmd.Key, customCmd.Value);

            if (txtCMD.Text != "")
            {
                if (blankLine && chunks.Count > 0)
                    print("");

                if (echo && lblPrompt.Text.ToLower() != "python>")
                {
                    print(txtCMD.Text, echoColor); //echo
                    flush(); //echo print should be instantaneous
                }
            }

            switch (inputMode)
            {
                case InputMode.AwaitingUsername: //first time running -> awaiting username
                    bool firstRun = user == "first_run";

                    if (txtCMD.Text == "")
                        print("Please enter your name.", errorColor);
                    else
                    {
                        user = txtCMD.Text;
                        saveOptions();

                        if (firstRun)
                        {
                            //continue with first time intro
                            greeting();
                            print("This seems to be the first time you are running Adjutant. Would you like to run the tutorial? (y/n)");

                            tutorialStep = 0;
                            inputMode = InputMode.Tutorial;
                        }
                        else
                            inputMode = InputMode.Default;

                        setPrompt();
                    }
                    break;
                case InputMode.TwitterPIN: //authorizing Twitter
                    if (txtCMD.Text == "")
                        return;

                    if (!twitter.FinalizeAuthorization(txtCMD.Text, out token, out secret))
                        printError("Authorization failed. Please try again.", twitter.TwException);
                    else
                    {
                        saveOptions();
                        print("Adjutant has been successfully authorized.");

                        //get new tweets
                        if (twitter.GetNewTweets(lastTweet))
                            lastTweet = twitter.GetLastTweet();
                        else
                            printError("Could not get a list of new tweets.", twitter.TwException);

                        tweetCount();

                        if (twitter.AnyNewTweets())
                            twitterPrint();
                    }

                    inputMode = InputMode.Default;
                    setPrompt();
                    break;
                case InputMode.ProcessRedirect: //forwarding input to external process
                    if (proc != null)
                        proc.StandardInput.WriteLine(txtCMD.Text);
                    else
                    {
                        string procName = lblPrompt.Text;
                        if (procName.Length > 0)
                            procName = procName.Substring(0, procName.Length - 1);

                        print("The process " + procName + " has terminated.", errorColor);

                        inputMode = InputMode.Default;
                        setPrompt();
                    }
                    break;
                case InputMode.Tutorial: //starting tutorial mode
                    if (txtCMD.Text.ToLower() == "exit")
                    {
                        inputMode = InputMode.Default;
                        setPrompt();
                        print("Tutorial canceled.");
                    }
                    else
                    {
                        //tutorial steps
                        switch (tutorialStep)
                        {
                            case 0:
                                if (txtCMD.Text.ToLower() == "y" || txtCMD.Text.ToLower() == "yes")
                                {
                                    tutorialStep++;

                                    printHelp("To stop the tutorial type \"exit\" at any time.<pause>");
                                    printHelp("");

                                    printHelp("Adjutant is a versatile and customizable console application, similar to Windows Command Prompt.");
                                    printHelp("For example, you can browse your computer with the standard command \"cd [directory]\".<pause>");
                                    printHelp("Adjutant also allows you to save commands or blocks of text into custom variables.");
                                    printHelp("For the purposes of this tutorial, Adjutant has created a variable called \"$path\" with the Adjutant directory as its value.<pause>");
                                    printHelp("Open that directory by typing the following command: \"cd $path\".");
                                }
                                else
                                {
                                    inputMode = InputMode.Default;
                                    setPrompt();
                                }
                                break;
                            case 1:
                                if (txtCMD.Text.ToLower() == "cd " + customCmds["$path"].ToLower())
                                {
                                    cd(customCmds["$path"]);
                                    tutorialStep++;

                                    printHelp("To add your own custom commands and variables use the \"custom\" command. Type \"help custom\" to find out more (when you finish the tutorial).<pause>");
                                    printHelp("");
                                    printHelp("Now open the \"tutorial\" subdirectory with the \"cd\" command: \"cd tutorial\".");
                                }
                                break;
                            case 2:
                                if (txtCMD.Text.ToLower() == "cd tutorial")
                                {
                                    cd(customCmds["$path"] + "\\tutorial");
                                    tutorialStep++;

                                    printHelp("In this directory you will find several files to interact with, as you would using Command Prompt.<pause>");
                                    printHelp("For example, open a file by typing its name: \"example.txt\"");
                                }
                                break;
                            case 3:
                                if (txtCMD.Text.ToLower() == "example.txt")
                                {
                                    runProcess("example.txt");
                                    tutorialStep++;

                                    printHelp("Besides the basic Command Prompt commands, Adjutant also has several advanced features, such as a Todo task manager, a Twitter client, and a Gmail client.<pause>");
                                    printHelp("To manage your personal tasks, use the \"todo\" and \"done\" commands.<pause>");
                                    printHelp("To initialize Twitter module type \"twitter /init\"<pause>");
                                    printHelp("To setup your Gmail account checker type \"mail /setup [username] [password]\"<pause>");
                                    printHelp("Adjutant's help system can tell you more about those modules, as well as other commands.<pause>");
                                    printHelp("");
                                    printHelp("To see a list of all commands, enter \"help\".<pause>");
                                    printHelp("To learn about a command in more detail, type \"help [command]\".<pause>");
                                    printHelp("Also note that ALL command switches can be used by just typing their initial letter.<pause>For example: \"/erase\" and \"/e\" perform the same switch operation.<pause>");
                                    printHelp("");
                                    printHelp("Adjutant's default output behaviour is to print 3 characters at a time. To flush out the current output press \"Enter\" while the output is being printed.");
                                    printHelp("You can change the default behaviour in the Options.<pause>");
                                    printHelp("");
                                    printHelp("You can use the Options window to change various other preferences, such as colors, sound notifications, running at Windows startup, etc.<pause> The Options tutorial will give you more information.");
                                    printHelp("To go to the Options just type \"options\" in the console, or right-click on it and select Options.<pause>");
                                    printHelp("");
                                    print("If you have any questions, suggestions, or bug reports, send me an email: ", false); print("winterstark@gmail.com", "mailto:winterstark@gmail.com", Color.Blue);
                                    printHelp("Have fun using Adjutant!");

                                    inputMode = InputMode.Default;
                                    setPrompt();
                                }
                                break;
                        }
                    }
                    break;
                default: //standard command input
                    try
                    {
                        string[] cmd = txtCMD.Text.Split(' ');

                        if (cmd.Length == 0)
                            return;

                        switch (cmd[0].ToLower())
                        {
                            case "help":
                                cmdHelp(cmd);
                                break;
                            case "exit":
                                Application.Exit();
                                break;
                            case "about":
                                print("Adjutant v" + VERSION + Environment.NewLine + "(c) " + YEAR + " Stanislav Žužić");
                                break;
                            case "options":
                                showOptions();
                                break;
                            case "date":
                                print(DateTime.Now.ToString("yyyy-MM-dd"));
                                break;
                            case "time":
                                print(DateTime.Now.ToString("HH:mm:ss"));
                                break;
                            case "cls":
                                cls();
                                break;
                            case "prompt":
                                prompt = !prompt;
                                setPrompt();
                                saveOptions();
                                break;
                            case "cd":
                                if (cmd.Length > 1)
                                {
                                    if (Directory.Exists(dir + cmd[1]))
                                        cd(dir + cmd[1]);
                                    else if (Directory.Exists(cmd[1]))
                                        cd(cmd[1]);
                                    else
                                        print("The system cannot find the path specified.");
                                }
                                break;
                            case "cd..":
                                int ind = dir.LastIndexOf("\\", dir.LastIndexOf("\\") - 1);
                                if (ind != -1)
                                    cd(dir.Substring(0, ind + 1));
                                break;
                            case "calc":
                                Process.Start("calc");
                                break;
                            case "custom":
                                cmdCustomCommand(cmd);
                                break;
                            case "todo":
                                cmdTodo(cmd);
                                break;
                            case "done":
                                cmdDone(cmd);
                                break;
                            case "mail":
                                cmdMail(cmd);
                                break;
                            case "tutorial":
                                //create $path
                                if (!customCmds.ContainsKey("$path"))
                                    customCmds.Add("$path", Application.StartupPath);
                                
                                //init tutorial
                                tutorialStep = 0;
                                inputMode = InputMode.Tutorial;
                                setPrompt();

                                print("Run the tutorial? (y/n)");
                                break;
                            case "twitter":
                            case "tw":
                                cmdTwitter(cmd);
                                break;
                            case "user":
                                print("Your current username: " + user);
                                print("Enter new username: ");
                                inputMode = InputMode.AwaitingUsername;
                                setPrompt();
                                break;
                            case "pad":
                                cmdPad(cmd);
                                break;
                            case "reddit":
                                cmdReddit(cmd);
                                break;
                            case "weather":
                                cmdWeather(cmd);
                                break;
                            case "":
                                flush();
                                break;
                            default:
                                runProcess(txtCMD.Text);
                                break;
                        }
                    }
                    catch (Exception exc)
                    {
                        printError("Error while executing command.", exc);
                    }
                    break;
            }

            //add line to command history and clear
            if (txtCMD.Text != "")
            {   
                history.Add(txtCMD.Text);
                historyInd = 0;
            }

            txtCMD.Text = "";
        }

        void cmdHelp(string[] cmd)
        {
            if (cmd.Length == 1)
            {
                print("List of Adjutant commands: " + Environment.NewLine);
                foreach (string command in new string[] { "about", "calc", "cd", "cls", "custom", "date", "done", "exit", "help", "mail", "pad", "prompt", "reddit", "time", "todo", "tutorial", "twitter", "user", "weather" })
                    print(command, helpColor);

                printHelp("For more information on a specific command, type \"help [COMMAND]\"<pause>");
                printHelp("");
                printHelp("Also note that ALL command switches can be used by just typing their initial letter.<pause>For example: \"/erase\" and \"/e\" perform the same switch operation.");
            }
            else
            {
                switch (cmd[1])
                {
                    case "about":
                        printHelp("Displays information about Adjutant.");
                        break;
                    case "calc":
                        printHelp("Starts Windows Calculator.");
                        break;
                    case "cd":
                        printHelp("Changes current directory.");
                        break;
                    case "cls":
                        printHelp("Clears screen (console).");
                        break;
                    case "custom":
                        printHelp("Use this command to save your custom commands, file or folder paths, URLs, or any other string constants.");
                        printHelp("Add a new custom command with the following syntax: \"custom [command_name]=[command_string]\".");
                        printHelp("To view the current custom commands just call the command without any other arguments: \"custom\".");
                        printHelp("To delete a custom command use this syntax: \"custom /del [command_name]\".");
                        break;
                    case "date":
                        printHelp("Gets the current date.");
                        break;
                    case "done":
                        printHelp("Use \"done\" to mark tasks in your todo lists as completed.");
                        printHelp("\"done [task_index]\" marks the task with that index (in today's todo list) as finished.");
                        printHelp("\"done [task_index] /tomorrow\" marks the task with that index (in tomorrow's todo list) as finished.");
                        printHelp("\"done [task_index] /date=YYYY-MM-DD\" marks the task with that index (in a specific date's todo list) as finished.");
                        printHelp("You can specify more than one index at a time by separating them with commas.");
                        printHelp("To specify a sequential range of numbers separate the bounds with a hyphen.");
                        printHelp("For example: \"done 4,8,15-16,23,42,100-108\"");
                        printHelp("Use the \"/undo\" switch to make a done item \"todo\" again. You can only undo one item at a time.");
                        printHelp("Use the \"/erase\" switch to remove todo items instead of marking them as read. This action is permanent and can only be done one item at a time.");
                        printHelp("");
                        printHelp("To change other settings, go to the Options menu.");
                        printHelp("To add new tasks or view task lists use the \"todo\" command.");
                        break;
                    case "exit":
                        printHelp("Shuts down Adjutant.");
                        break;
                    case "help":
                        printHelp("H E L P C E P T I O N");
                        break;
                    case "mail":
                        printHelp("The \"mail\" command allows you to check your Gmail account for new messages.");
                        printHelp("To setup your account, use the following command: \"mail /setup [username] [password]\".");
                        printHelp("Calling the command will display the list of new mail.");
                        printHelp("Calling the command with the \"\\verbose\" switch will display the list of new mail, as well as the first segment of each mail.");
                        printHelp("Use \"mail \\refresh\" to manually check for new mail.");
                        printHelp("To change how often Adjutant checks for new mail, as well as other options, go to the Options menu.");
                        break;
                    case "time":
                        printHelp("Gets the current time.");
                        break;
                    case "todo":
                        printHelp("The \"todo\" command implements a simple todo list manager.");
                        printHelp("Use it to add new tasks or view task lists.");
                        printHelp("\"todo [task]\" adds a new task for today.");
                        printHelp("\"todo [task] /tomorrow\" adds a new task to tomorrow's todo list.");
                        printHelp("\"todo [task] /date=YYYY-MM-DD\" adds a new task for a specific date.");
                        printHelp("\"todo\" displays unfinished tasks for today.");
                        printHelp("\"todo /yesterday\" displays yesterday's todo list.");
                        printHelp("\"todo /tomorrow\" displays unfinished tasks for tomorrow.");
                        printHelp("\"todo /date=YYYY-MM-DD\" displays unfinished tasks for a specific day.");
                        printHelp("\"todo /list\" displays all todo lists from this date onward.");
                        printHelp("\"todo /archive\" displays past todo lists.");
                        printHelp("");
                        printHelp("To change other settings, go to the Options menu.");
                        printHelp("To check tasks as completed use the \"done\" command.");
                        break;
                    case "twitter":
                    case "tw":
                        if (cmd.Length < 3 || !cmd[2].ToLower().Contains("/i"))
                        {
                            printHelp("This command allows you to read the tweets in your home timeline.");
                            printHelp("You can also use the shorter keyword \"tw\".");
                            printHelp("");
                            printHelp("Calling the command will display a tweet (if there are any) and will lock input into Adjutant.");
                            printHelp("Press \"o\" to open all URLs in the tweet.");
                            printHelp("Press \"m\" to open all mentions and hashtags in the tweet.");
                            printHelp("Press \"u\" to open the user's profile.");
                            printHelp("Press \"t\" to open the tweet page.");
                            printHelp("Note that you can also click on usernames, URLs, hashtags, etc, to open them in your browser.");
                            printHelp("");
                            printHelp("Press \"j\" to read the next tweet.");
                            printHelp("Press \"k\" to go back to the previous tweet.");
                            printHelp("Press \"a\" to print all tweets at once.");
                            printHelp("Press \"Esc\" to exit Twitter mode and enable standard input again.");
                            printHelp("");
                            printHelp("You can also print out all tweets with the following command: \"twitter /all\"");
                            printHelp("");
                            printHelp("Before using the Twitter service you will have to authorize Adjutant to view your tweets.");
                            printHelp("The authorization should begin automatically; if not, enter the following command: \"twitter /init\"");
                            printHelp("If you run into any problems with authorization, you can get more information by typing the following: \"help twitter /init\"");
                            printHelp("To change other settings, go to the Options menu.");
                        }
                        else
                        {
                            printHelp("To authorize Adjutant to view your tweets, use the following command: \"twitter /init\"");
                            printHelp("A webpage will open in your default browser with a PIN code. Copy and paste the PIN into Adjutant to finish authorization.<pause>");
                            printHelp("");
                            printHelp("If the \"twitter\" command stopped working, there could be several different causes for the problem:");
                            printHelp("Your Internet connection could be down.");
                            printHelp("Twitter service could be temporarily unavailable.");
                            printHelp("Adjutant's authorization might no longer be valid. Try authorizing it again.");
                        }
                        break;
                    case "options":
                        printHelp("Opens the options window.");
                        break;
                    case "pad":
                        printHelp("The \"pad\" command turns Adjutant into a simple text editor.<pause>");
                        printHelp("\"pad\" opens a blank text editor.<pause>");
                        printHelp("\"pad [file]\" opens an existing text file.<pause>");
                        break;
                    case "reddit":
                        printHelp("Use the \"reddit\" command to browse Reddit submissions from Adjutant.<pause>");
                        printHelp("The command is currently very limited and only allows you to browse the currently hot posts in a particular subreddit.");
                        printHelp("Type \"reddit [subreddit]\" to browse submissions.<pause>");
                        printHelp("You can browse multiple subreddits by typing \"reddit [subreddit1]+[subreddit2]+...\".");
                        printHelp("While browsing Reddit use the following keyboard shortcuts:");
                        printHelp("Press \"j\" to to read the next submission.");
                        printHelp("Press \"k\" to to read the previous submission.");
                        printHelp("Press \"l\" to open the submitted link.");
                        printHelp("Press \"c\" to open the comments page.");
                        printHelp("Press \"u\" to open the OP's profile page.");
                        printHelp("Press \"r\" to open the subreddit.");
                        printHelp("Press \"Esc\" to open all mentions and hashtags in the tweet.");
                        break;
                    case "weather":
                        printHelp("The \"weather\" command can be used to display current and future weather conditions for your location.<pause>");
                        printHelp("Calling the command without parameters (\"weather\") will display current weather status.<pause>");
                        printHelp("Type \"weather /forecast\" to get the weather forecast for the next five days.<pause>");
                        printHelp("Type \"weather /hourly\" to get the weather details of every 3-hour period for the next 24 hours.<pause>");
                        printHelp("You can also specify the number of days for your forecast by including the parameter \"/days=N_DAYS\".<pause>The maximum number of days is 16 for \"/forecast\" and 5 for \"hourly\".");
                        printHelp("Customize your location and other weather settings in Options.");
                        break;
                    case "prompt":
                        printHelp("Toggles prompt.");
                        break;
                    case "user":
                        printHelp("Change your username with this command.");
                        printHelp("Currently, your username is only used when Adjutant starts and displays the welcome message.");
                        break;
                    case "tutorial":
                        printHelp("Learn how to use Adjutant.");
                        break;
                    default:
                        print("Unrecognized command.", errorColor);
                        break;
                }
            }
        }

        void cmdCustomCommand(string[] cmd)
        {
            if (cmd.Length == 1)
            {
                if (customCmds.Count == 0)
                    print("You currently have no custom commands.");
                else
                {
                    print("Current custom commands:");
                    foreach (var custom in customCmds)
                        print(custom.Key + "=" + custom.Value);
                }
            }
            else if (cmd.Length == 3 && cmd[1].Contains("/d"))
            {
                string delCmd = cmd[2];

                if (!customCmds.ContainsKey(delCmd))
                    print("There is no such custom command.", errorColor);
                else
                {
                    customCmds.Remove(delCmd);
                    saveCustomCmds();
                    print(delCmd + " successfully deleted.");
                }
            }
            else if (cmd.Length > 1)
            {
                string[] parts = txtCMD.Text.Substring(7).Split('=');

                if (parts.Length != 2)
                    print("Invalid format. Please specify custom command with the following syntax: \"custom [command_name]=[command_string]\"", errorColor);
                else
                {
                    //remove extra spaces from the beginning and end of the strings
                    parts[0] = cleanupString(parts[0]);
                    parts[1] = cleanupString(parts[1]);

                    //add cmd
                    customCmds.Add(parts[0], parts[1]);
                    saveCustomCmds();
                    print(parts[0] + "=" + parts[1] + " succesfully added.");
                }
            }
        }

        string cleanupString(string s)
        {
            while (s.Length > 1 && s[0] == ' ')
                s = s.Substring(1);
            while (s.Length > 1 && s[s.Length - 1] == ' ')
                s = s.Substring(0, s.Length - 1);

            return s;
        }

        void setCMD(string txt)
        {
            txtCMD.Text = txt;
            txtCMD.SelectionStart = txt.Length;
        }

        void windowAutosize()
        {
            txtCMD.Width = this.Width - lblPrompt.Width;
            txtCMD.Top = this.Height - txtCMD.Height;

            if (prompt)
                lblPrompt.Top = txtCMD.Top;

            this.Height = txtCMD.Top + txtCMD.Height;

            //calc line height
            lineH = (int)grafx.Graphics.MeasureString("A", txtCMD.Font).Height;
            
            Chunk.LineH = lineH;
            Chunk.ConsoleWidth = this.Width;

            if (outputMode == OutputMode.Pad)
            {
                sciPad.Width = this.Width;
                sciPad.Height = this.Height - padMenus.Height;
            }
        }

        void jumpToLastLine()
        {
            yOffset = 0;

            if (chunks.Count == 0 || lastChunk == -1)
                chunkOffset = 0;
            else
            {
                //go through chunks until their output wouldn't fit in the console (1 page of console output)
                chunkOffset = lastChunk;

                int chunkInd = chunkOffset + 1;
                int h = 0; //used for tracking the largest (tallest) chunk in a line
                int sumH = 0; //used for tracking the total height of processed lines

                while (h == 0) //h will become nonzero when a line doesn't fit into the console window (and the console window height cannot be increased)
                {
                    //any more lines?
                    chunkInd--;

                    if (chunkInd < 0)
                        break;

                    //scan through next line to find out its height and starting chunk
                    h = chunks[chunkInd].GetHeight();

                    while (chunkInd > 0 && !chunks[chunkInd - 1].IsNewline())
                    {
                        chunkInd--;

                        if (chunks[chunkInd].GetHeight() > h)
                            h = chunks[chunkInd].GetHeight();
                    }

                    if (sumH + h <= txtCMD.Top)
                    {
                        //this line fits into console window
                        sumH += h;
                        h = 0;

                        chunkOffset = chunkInd;
                    }
                    else
                    {
                        //is it possible to increase console window height?
                        int dH = sumH + h - txtCMD.Top;

                        if (this.Height + dH <= maxH)
                        {
                            //enlarge window
                            autoResize = true;
                            this.Height += dH;
                            windowAutosize();

                            //accept line
                            chunkOffset = chunkInd;
                            sumH += h;
                            h = 0;
                        }
                        else if (sumH < txtCMD.Top && h > lineH) //if there is empty space left and the line that doesn't fit contains images
                        {
                            //draw the line that doesn't fit with an offset (so the lower parts of the images will be visible)
                            chunkOffset = chunkInd;
                            yOffset = -h + (txtCMD.Top - sumH);
                        }
                    }
                }
            }
            
            //ensure adjutant remains hidden
            if (hidden && hideStyle == HideStyle.ScrollUp && !timerShowHide.Enabled)
                this.Top = -this.Height + 1;
        }

        void update()
        {
            jumpToLastLine();
            draw(grafx.Graphics);

            if (!this.InvokeRequired)
                this.Refresh();
            else
                this.Invoke(new MethodInvoker(this.Refresh));
        }

        void updateImage(Chunk chunk)
        {
            //one or more image chunks have finished downloading images
            //update only if not printing (otherwise update will be called as usual)
            if (!timerPrint.Enabled)
            {
                if (timerPrint.Interval == ZERO_DELAY || chunk.IsImgExpanding())
                    jumpToLastLine();

                //also force drawing
                this.Invalidate();
            }
        }

        void sendChunkToNewLine(Chunk chunk)
        {
            //make previous chunk have newline
            int ind = getChunkIndex(chunk);

            if (ind > 0)
                chunks[ind - 1].InsertNewline();
        }

        void checkIfBully(Chunk chunk, int x)
        {
            //check if image chunk grew so big it pushed the next chunk out of console window bounds
            int ind = getChunkIndex(chunk);

            if (ind != -1 && ind < chunks.Count - 1)
            {
                if (x + chunks[ind + 1].GetWidth() > this.Width)
                {
                    //send the next chunk to a new line
                    chunk.InsertNewline();
                    //jumpToLastLine();
                    //this.OnResize(new EventArgs());
                }
            }
        }

        int getChunkIndex(Chunk chunk)
        {
            int ind = 0;

            while (ind < chunks.Count)
            {
                if (chunks[ind] == chunk)
                    return ind;

                ind++;
            }

            return -1;
        }

        void forceConsoleResizeEvent()
        {
            this.OnResize(new EventArgs());
        }

        void cls()
        {
            //stop any current output
            timerPrint.Enabled = false;

            //cleanup chunk resources
            foreach (Chunk chunk in chunks)
                chunk.DisposeResources();
            
            //clear chunks
            chunks.Clear();
            lastChunk = 0;
            lastChunkChar = 0;

            //reset window height
            this.Height = minH;
            
            windowAutosize();
            update();
        }

        #region Printing procedures
        void print(string txt)
        {
            print(txt, "", true, txtCMD.ForeColor, false);
        }

        void print(string txt, bool newline)
        {
            print(txt, "", newline, txtCMD.ForeColor, false);
        }

        void print(string txt, Color color)
        {
            print(txt, "", true, color, false);
        }

        void print(string txt, bool newline, Color color)
        {
            print(txt, "", newline, color, false);
        }

        void print(string txt, bool newline, Color color, bool strikeout)
        {
            print(txt, "", newline, color, strikeout);
        }

        void print(string txt, string link, Color color)
        {
            print(txt, link, true, color, false);
        }

        void print(string txt, string link, bool newline, Color color)
        {
            print(txt, link, newline, color, false);
        }

        void print(string txt, string link, bool newline, Color color, bool strikeout)
        {
            if (txt == "")
                txt = " "; //blank line

            if (lastChunk == -1)
                lastChunk = 0;

            while (txt.Contains("<image="))
            {
                //split into chunks
                int lb = txt.IndexOf("<image=");

                if (lb != 0)
                    print(txt.Substring(0, lb), link, false, color, strikeout); //print text before image

                lb += 7;
                int ub = txt.IndexOf('>', lb);

                if (ub != -1)
                {
                    //print img
                    string imgLocation = txt.Substring(lb, ub - lb);

                    if (link == "")
                    {
                        link = imgLocation;

                        if (link.Contains("pbs.twimg.com"))
                            link += ":large"; //open large versions of Twitter images
                    }

                    Chunk imgChunk = new Chunk(imgLocation, link, newline && ub == txt.Length - 1, newline);

                    //can img fit in current line?
                    if (leftMargin + imgChunk.GetWidth(true) > this.Width && chunks.Count > 0)
                        chunks[chunks.Count - 1].InsertNewline(); //nope, send img to next line

                    chunks.Add(imgChunk);

                    if (newline && ub == txt.Length - 1)
                        leftMargin = 0;
                    else
                        leftMargin += imgChunk.GetWidth(true);

                    showNewChunks();

                    txt = txt.Substring(ub + 1);
                    if (txt == "")
                        return;
                }
                else
                    break;
            }

            if (timerPrint.Interval == ZERO_DELAY)
                txt = txt.Replace("<pause>", "");
            else
                txt = txt.Replace("<pause>", Environment.NewLine + "<pause>" + Environment.NewLine);

            string[] lines = txt.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                bool nLine = i == lines.Length - 1 ? newline : true;
                List<Chunk> newChunks = Chunk.Chunkify(lines[i], link, color, strikeout, leftMargin, nLine, nLine);

                chunks.AddRange(newChunks);

                if (nLine)
                    leftMargin = 0;
                else
                {
                    if (newChunks.Count > 1)
                        leftMargin = 0;

                    leftMargin += newChunks[newChunks.Count - 1].GetWidth();
                }
            }

            showNewChunks();
        }

        void showNewChunks()
        {
            if (timerPrint.Interval != ZERO_DELAY)
            {
                jumpToLastLine();
                timerPrint.Enabled = true;
            }
            else
            {
                if (!timerPrint.Enabled)
                {
                    lastChunk = chunks.Count - 1;
                    lastChunkChar = chunks[lastChunk].ToString().Length;
                }

                update();
            }
        }

        void printError(string msg, Exception exc)
        {
            print(msg + "<pause>", errorColor);
            print(exc.Message, errorColor);
        }

        void printHelp(string msg)
        {
            if (msg != "")
            {
                //prints quoted segments (e.g. "command [arg1] [arg2] /switch") in helpColor
                string[] segments = msg.Split(new char[] { '"' });

                for (int i = 0; i < segments.Length; i++)
                    if (i % 2 == 1) //print every other segment in helpColor
                        print(segments[i], i == segments.Length - 1, helpColor);
                    else
                        print(segments[i], i == segments.Length - 1);
            }
            else
                print(""); //newline
        }
        #endregion

        void flush()
        {
            if (chunks.Count > 0)
            {
                for (int i = lastChunk; i < chunks.Count; i++)
                    if (chunks[i].ToString() == "<pause>")
                        chunks.RemoveAt(i--);

                lastChunk = chunks.Count - 1;
                lastChunkChar = chunks[lastChunk].ToString().Length;
            }
        }

        void cd(string path)
        {
            dir = path;
            if (dir[dir.Length - 1] != '\\')
                dir += "\\";

            if (lblPrompt.Visible)
                setPrompt(dir + ">");
        }

        void runProcess(string cmd)
        {
            //separate process name from arguments
            string procName, args;
            int ub = cmd.IndexOf('"', 1);

            if (cmd[0] == '"' && ub != -1)
            {
                //process name is enclosed within quotation marks
                procName = cmd.Substring(1, ub - 1);

                if (cmd.Length > ub + 1)
                    args = cmd.Substring(ub + 1);
                else
                    args = "";
            }
            else if (cmd.Contains(' '))
            {
                //args are separated by a space
                ub = cmd.IndexOf(' ');

                procName = cmd.Substring(0, ub);

                if (cmd.Length > ub + 1)
                    args = cmd.Substring(ub + 1);
                else
                    args = "";
            }
            else
            {
                //no args
                procName = cmd;
                args = "";
            }

            //procName missing directory?
            if (!File.Exists(procName) && File.Exists(dir + procName))
                procName = dir + procName;

            //determine type of process
            ProcessStartInfo procInfo;

            if (File.Exists(procName) && Path.GetExtension(procName) != ".vbs")
            {
                if (Path.GetExtension(procName) == ".py")
                {
                    //it's a python script
                    //if (padLanguages[".py"] != "")
                    //{
                        procInfo = new ProcessStartInfo(@"cmd.exe", "/C \"" + procName + "\" " + args);
                        //procInfo = new ProcessStartInfo(python, " \"" + procName + "\" " + args);
                        inputMode = InputMode.ProcessRedirect;
                    //}
                    //else
                    //{
                    //    print("Could not start script.<pause>Python was not found on your system.", errorColor);
                    //    return;
                    //}
                }
                else
                {
                    //it's a file (but not a vbs/py script)
                    procInfo = new ProcessStartInfo(procName, args);
                    inputMode = InputMode.ProcessRedirect;
                }
            }
            else
            {
                int temp;
                string url = getNextURL(procName, out temp);

                if (url != "")
                {
                    //it's a url
                    Process.Start(url);
                    return;
                }
                else
                {
                    if (Path.GetExtension(procName) == ".vbs")
                        cmd = "cscript " + cmd; //force vbs scripts to use cscript (so that the process output returns to Adjutant instead of being displayed as a message box)

                    //send it to command prompt
                    //cmd = @"c:\dev\test\Hello2.exe";
                    procInfo = new ProcessStartInfo(@"cmd.exe", @"/C " + cmd);
                }
            }

            //prepare process and run
            procInfo.WindowStyle = ProcessWindowStyle.Hidden;
            procInfo.WorkingDirectory = dir;
            procInfo.RedirectStandardOutput = true;
            procInfo.RedirectStandardInput = true;
            procInfo.RedirectStandardError = true;
            procInfo.UseShellExecute = false;
            procInfo.CreateNoWindow = true;
            
            //if (inputMode == InputMode.ProcessRedirect)
            if (true)
            {
                //set events for process output and redirect user input to the process
                setPrompt(Path.GetFileNameWithoutExtension(procInfo.FileName) + ">");

                if (outputMode == OutputMode.Pad)
                    print("");

                programStarted = DateTime.Now;
                proc = Process.Start(procInfo);

                proc.OutputDataReceived += new DataReceivedEventHandler(proc_DataReceived);
                proc.ErrorDataReceived += new DataReceivedEventHandler(proc_ErrorDataReceived);
                proc.Exited += new EventHandler(proc_Exited);

                proc.EnableRaisingEvents = true;
                proc.SynchronizingObject = this;

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            else
            {
                //just print the output
                proc = Process.Start(procInfo);

                if (!File.Exists(procName) || Path.GetExtension(procName).ToLower() == ".exe")
                {
                    //wait for output of .exe files
                    StreamReader outputStream = proc.StandardOutput;
                    string output = outputStream.ReadToEnd();
                    proc.WaitForExit(1000);

                    if (output != "")
                        print(output);
                    else
                        print("Unrecognized command.", errorColor);
                }
            }
        }

        private void proc_DataReceived(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (outLine.Data != null)
                print(outLine.Data);
        }

        private void proc_ErrorDataReceived(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (outLine.Data != null)
                print(outLine.Data, errorColor);
        }

        private void proc_Exited(object sender, System.EventArgs e)
        {
            inputMode = InputMode.Default;

            if (outputMode == OutputMode.Pad)
                print("[Finished in " + Math.Round(DateTime.Now.Subtract(programStarted).TotalSeconds, 1).ToString().Replace(',', '.') + "s. Press Esc to return to Pad]", echoColor);
            else
                setPrompt();
        }
         
        string getFilteredPaths(string txt, bool prev)
        {
            //autocomplete filename
            if (filteredPaths == null || (!filteredPaths.Contains(txt) && !filteredPaths.Contains(dir + txt)))
            {
                //build new filtered paths list
                string targetDir = dir, targetPattern = txt;

                if (txt.Length >= 3 && txt.Substring(1, 2) == ":\\")
                {
                    targetDir = txt.Substring(0, txt.LastIndexOf("\\") + 1);

                    if (txt[txt.Length - 1] == '\\')
                        targetPattern = "";
                    else
                        targetPattern = txt.Substring(txt.LastIndexOf("\\") + 1);
                }
                else if (txt.Contains('\\'))
                {
                    targetDir += txt.Substring(0, txt.LastIndexOf("\\") + 1);
                    targetDir = targetDir.Replace("\\\\", "\\");
                    targetPattern = txt.Substring(txt.LastIndexOf("\\") + 1);
                }

                if (!Directory.Exists(targetDir))
                    return txt;

                if (txtCMD.Text.Length > 2 && txtCMD.Text.Substring(0, 2).ToLower() == "cd")
                    filteredPaths = Directory.GetDirectories(targetDir, targetPattern + "*");
                else
                {
                    string[] filteredFiles = Directory.GetFiles(targetDir, targetPattern + "*");
                    string[] filteredDirs = Directory.GetDirectories(targetDir, targetPattern + "*");
                    filteredPaths = new string[filteredFiles.Length + filteredDirs.Length];

                    int fileInd = 0, dirInd = 0;
                    while (fileInd + dirInd < filteredPaths.Length)
                        if (dirInd == filteredDirs.Length || (fileInd < filteredFiles.Length && filteredFiles[fileInd].CompareTo(filteredDirs[dirInd]) < 0))
                            filteredPaths[fileInd + dirInd] = filteredFiles[fileInd++];
                        else
                            filteredPaths[fileInd + dirInd] = filteredDirs[dirInd++];
                }

                if (filteredPaths.Length == 0)
                {
                    filteredPaths = null;
                    return txt;
                }
                else
                {
                    if (!prev)
                        tabInd = 0;
                    else
                        tabInd = filteredPaths.Length - 1;
                }
            }
            else
            {
                //select next/prev path
                if (!prev)
                {
                    tabInd++;
                    if (tabInd == filteredPaths.Length)
                        tabInd = 0;
                }
                else
                {
                    tabInd--;
                    if (tabInd == -1)
                        tabInd = filteredPaths.Length - 1;
                }
            }

            //get only relevant part of path
            string path = filteredPaths[tabInd];

            if (path.Length > dir.Length && path.Substring(0, dir.Length) == dir)
                path = path.Substring(dir.Length);

            //enclose with quotes for multi-word paths
            if (path.Contains(' '))
                path = "\"" + path + "\"";

            return path;
        }

        void mouseDown(int mx, int my)
        {
            if (outputMode == OutputMode.Pad)
            {
                //pad's menu strip interferes with resizing so it's handled as a special case in ActivityHook_OnMouseActivity
                if (this.Cursor == Cursors.SizeNWSE)
                {
                    padResizingW = true;
                    padResizingH = true;
                }
                else if (this.Cursor == Cursors.SizeWE)
                    padResizingW = true;
                else if (this.Cursor == Cursors.SizeNS)
                    padResizingH = true;
                else
                {
                    prevX = mx;
                    prevY = my;
                    drag = true;
                }
            }
            else
            {
                if (this.Cursor == Cursors.SizeNWSE)
                {
                    prevX = this.Width;
                    prevY = this.Height;
                    resizeW = true;
                    resizeH = true;
                }
                else if (this.Cursor == Cursors.SizeWE)
                {
                    prevX = this.Width;
                    resizeW = true;
                }
                else if (this.Cursor == Cursors.SizeNS)
                {
                    prevY = this.Height;
                    resizeH = true;
                }
                else
                {
                    prevX = mx;
                    prevY = my;
                    drag = true;
                }

                if (resizeH)
                    prevH = this.Height;
            }
        }

        void mouseMove(int mx, int my)
        {
            if (hidden)
            {
                //unhide adjutant
                hiding = false;
                timerShowHide.Enabled = true;
                SetForegroundWindow(this.Handle);
            }

            if (drag)
            {
                if (Math.Abs(this.Top + my - prevY) < 10)
                    this.Top = 0;
                else if (Math.Abs(this.Top + my - prevY + this.Height - Screen.PrimaryScreen.WorkingArea.Height) < 10)
                    this.Top = Screen.PrimaryScreen.WorkingArea.Height - this.Height;
                else
                    this.Top += my - prevY;

                if (Math.Abs(this.Left + mx - prevX) < 10)
                    this.Left = 0;
                else if (Math.Abs(this.Left + mx - prevX + this.Width - Screen.PrimaryScreen.WorkingArea.Width) < 10)
                    this.Left = Screen.PrimaryScreen.WorkingArea.Width - this.Width;
                else
                    this.Left += mx - prevX;
            }
            else if (resizeW || resizeH)
            {
                if (resizeW)
                {
                    if (Math.Abs(this.Left + mx - Screen.PrimaryScreen.WorkingArea.Width) < 10)
                        this.Width = Screen.PrimaryScreen.WorkingArea.Width - this.Left;
                    else
                        this.Width = mx;
                }

                if (resizeH)
                {
                    if (Math.Abs(this.Top + my - Screen.PrimaryScreen.WorkingArea.Height) < 10)
                        this.Height = Screen.PrimaryScreen.WorkingArea.Height - this.Top;
                    else
                        this.Height = my + (outputMode == OutputMode.Pad ? 0 : 0); //dragging in Pad mode is problematic for some reason and needs a little extra wiggle room
                }

                draw(grafx.Graphics);
                this.Refresh();
            }
            else
            {
                if (mx > this.Width - 5 && my > this.Height - 5)
                    this.Cursor = Cursors.SizeNWSE;
                else if (mx > this.Width - 5)
                    this.Cursor = Cursors.SizeWE;
                else if (my > this.Height - 5)
                    this.Cursor = Cursors.SizeNS;
                else
                    this.Cursor = outputMode == OutputMode.Pad ? Cursors.IBeam : Cursors.Default;
            }
        }

        void mouseUp(int mx, int my)
        {
            if (drag)
            {
                x = this.Left;
                y = this.Top;
            }
            else if (outputMode == OutputMode.Pad)
            {
                padW = this.Width;
                padH = this.Height;

                if (resizeH)
                    windowAutosize();
            }
            else if (resizeH)
            {
                if (!ctrlKey)
                    minH = this.Height;
                else
                    maxH = this.Height;

                windowAutosize();
            }

            if (drag || resizeW || resizeH)
            {
                saveOptions();
                initGrafx();

                if (ctrlKey)
                {
                    this.Height = prevH;
                    windowAutosize();
                }
            }

            drag = false;
            resizeW = false;
            resizeH = false;
        }

        void autohideTrigger()
        {
            if (autoHideDelay != 0)
            {
                autoHide = DateTime.Now.AddSeconds(autoHideDelay);
                timerAutohide.Enabled = true;
            }
        }

        #region Todo Module
        string todoFile(string when)
        {
            switch (when)
            {
                case "today":
                    return todoDir + "todo_" + DateTime.Today.ToString("yyyy-MM-dd") + ".txt";
                case "tomorrow":
                    return todoDir + "todo_" + DateTime.Today.AddDays(1).ToString("yyyy-MM-dd") + ".txt";
                case "previous":
                    List<string> todos = Directory.GetFiles(todoDir, "todo_*-*-*.txt").ToList();

                    int todaysTodo = todos.IndexOf(todoFile("today"));
                    if (todaysTodo == -1)
                        todaysTodo = todos.Count;

                    if (todaysTodo >= 1)
                        return todos[todaysTodo - 1];
                    else
                        return "";
                default:
                    return todoDir + "todo_" + when + ".txt";
            }
        }

        List<string> tasklist(string filename)
        {
            if (!File.Exists(filename))
                return null;

            StreamReader file = new System.IO.StreamReader(filename);
            string contents = file.ReadToEnd();
            file.Close();

            return contents.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        void todoLoad()
        {
            //find or create todos dir
            if (todoDir.Length > 0 && todoDir[todoDir.Length - 1] != '\\')
                todoDir += "\\";

            if (!Directory.Exists(todoDir))
            {
                todoDir = Application.StartupPath + "\\" + todoDir;

                if (!Directory.Exists(todoDir))
                    Directory.CreateDirectory(todoDir);
            }

            //find or create today's todo list
            if (File.Exists(todoFile("today")))
                todo = tasklist(todoFile("today"));
            else
                todo = new List<string>();

            if (todoAutoTransfer)
            {
                //check last todo list for unfinished items
                List<string> todos = Directory.GetFiles(todoDir, "todo_*-*-*.txt").ToList();
                int todaysTodo = todos.IndexOf(todoFile("today"));
                if (todaysTodo == -1)
                    todaysTodo = todos.Count;

                todo.AddRange(tasklist(todoFile("previous")).FindAll(item => !item.Contains("__DONE__") && !todo.Contains(item) && !todo.Contains("__DONE__" + item)));

                StreamWriter file = new StreamWriter(todoFile("today"));
                foreach (string item in todo)
                    file.WriteLine(item);
                file.Close();
            }

            todoShow();
        }

        void todoAppend(string filename, string task)
        {
            StreamWriter file = new System.IO.StreamWriter(filename, true);
            file.WriteLine(task);
            file.Close();
        }

        void todoShow()
        {
            if (todo == null || todo.Count == 0 || (todoHideDone && !todo.Any(t => t.Length < 8 || t.Substring(0, 8) != "__DONE__")))
                print("Your todo list for today is empty.", todoMiscColor);
            else
            {
                print("To do:", todoFile("today"), todoMiscColor);

                int ind = 1;
                foreach (string task in todo)
                    if (task.Contains("__DONE__"))
                    {
                        if (!todoHideDone)
                        {
                            print(ind++ + ". " + task.Replace("__DONE__", ""), false, todoDoneColor, true);
                            print(" ✓", todoDoneColor);
                        }
                    }
                    else
                        print(ind++ + ". " + task, todoItemColor);
            }
        }

        void todoShow(string date)
        {
            DateTime dt;
            if (DateTime.TryParse(date, out dt))
            {
                if (dt.Date == DateTime.Today)
                    date = "today";
                else if (dt.Date == DateTime.Today.AddDays(1))
                    date = "tomorrow";
            }

            if (date == DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"))
                print("Todo list for yesterday:", todoFile(date), todoMiscColor);                
            else
                print("Todo list for " + date + ":", todoFile(date), todoMiscColor);

            List<string> tasks = tasklist(todoFile(date));

            if (tasks != null)
            {
                int ind = 1;
                foreach (string task in tasklist(todoFile(date)))
                    if (task.Contains("__DONE__"))
                    {
                        print(ind++ + ". " + task.Replace("__DONE__", ""), false, todoDoneColor, true);
                        print(" ✓", todoDoneColor);
                    }
                    else
                        print(ind++ + ". " + task, todoItemColor);
            }
            else
                print("Todo file does not exist for specified date.", errorColor);
        }

        void cmdTodo(string[] cmd)
        {
            string date, task;

            if (cmd.Length == 1)
                todoShow();
            else if (cmd[1].ToLower().Contains("/l"))
            {
                foreach (string file in Directory.GetFiles(todoDir, "todo_*-*-*.txt"))
                {
                    date = file.Substring(file.LastIndexOf('\\') + 1).Substring(5, 10);

                    if (DateTime.Parse(date) >= DateTime.Today)
                        todoShow(date);
                }
            }
            else if (cmd[1].ToLower().Contains("/y"))
                todoShow(DateTime.Now.AddDays(-1).Date.ToString("yyyy-MM-dd"));
            else if (cmd[1].ToLower().Contains("/a"))
            {
                foreach (string file in Directory.GetFiles(todoDir, "todo_*-*-*.txt"))
                {
                    date = file.Substring(file.LastIndexOf('\\') + 1).Substring(5, 10);

                    if (DateTime.Parse(date) < DateTime.Today)
                        todoShow(date);
                }
            }
            else
            {
                task = txtCMD.Text.Substring(txtCMD.Text.IndexOf(' ') + 1);

                if (task.Contains("/t"))
                {
                    task = task.Substring(0, task.IndexOf('/'));

                    if (task != "")
                    {
                        todoAppend(todoFile("tomorrow"), task);

                        print(task, false, todoItemColor);
                        print(" added in tomorrow's todo list.", todoMiscColor);
                    }
                    else
                        todoShow("tomorrow");
                }
                else if (task.Contains("/d"))
                {
                    int lb = task.IndexOf('=', task.IndexOf("/d"));

                    if (lb != -1)
                    {
                        date = task.Substring(lb + 1);
                        task = task.Substring(0, task.IndexOf('/'));

                        if (task != "")
                        {
                            todoAppend(todoFile(date), task);

                            print(task, false, todoItemColor);
                            print(" added in the todo list for date: " + date + ".", todoMiscColor);
                        }
                        else
                            todoShow(date);
                    }
                }
                else
                {
                    todo.Add(task);
                    todoAppend(todoFile("today"), task);

                    print(task, false, todoItemColor);
                    print(" added in today's todo list.", todoMiscColor);

                    todoShow();
                }
            }
        }

        void cmdDone(string[] cmd)
        {
            //separate task index (or indices) from switches (if any)
            string task = txtCMD.Text.Substring(txtCMD.Text.IndexOf(' ') + 1);
            string date = "today";
            bool erase = false, undo = false;

            if (task.Contains('/'))
            {
                //make note of switches
                if (task.Contains("/e"))
                    erase = true;
                else if (task.Contains("/u"))
                    undo = true;
                
                if (task.Contains("/t"))
                    date = "tomorrow";
                else if (task.Contains("/d"))
                {
                    int lb = task.IndexOf('=', task.IndexOf("/d"));

                    if (lb != -1)
                    {
                        lb++;

                        int ub = task.IndexOf(" ", lb);
                        if (ub == -1)
                            ub = task.Length;

                        date = task.Substring(lb, ub - lb);
                    }
                }

                //cleanup switches
                while (task.Contains('/'))
                {
                    int lb = task.IndexOf('/');
                    int ub = task.IndexOf(' ', lb + 2);
                    if (ub == -1)
                        ub = task.Length;

                    task = cleanupString(task.Remove(lb, ub - lb));
                }
            }

            List<string> tasks = tasklist(todoFile(date)); //load target todo list

            if (tasks == null)
            {
                print("Todo file does not exist for specified date.", errorColor);
                return;
            }

            if (undo)
            {
                //make a done item be "todo" again
                int taskInd;
                if (!int.TryParse(task, out taskInd))
                {
                    print("Invalid argument. Expected integer (index of todo item).", errorColor);
                    print("You can only undo one todo item at a time.", errorColor);
                }
                else
                {
                    taskInd--;

                    if (taskInd < 0 || taskInd >= tasks.Count) //check if item index exists in list
                        print("Task no. " + taskInd + " doesn't exist in that todo file.", errorColor);
                    else if (!tasks[taskInd].Contains("__DONE__")) //check if item's status is done
                        print("Task no. " + taskInd + " is not done. You can only undo done items.", errorColor);
                    else
                    {
                        print("Undo task: " + tasks[taskInd]);
                        tasks[taskInd] = tasks[taskInd].Replace("__DONE__", "");
                    }
                }
            }
            else if (erase)
            {
                //delete todo item
                int taskInd;
                if (!int.TryParse(task, out taskInd))
                {
                    print("Invalid argument. Expected integer (index of todo item).", errorColor);
                    print("You can only delete one todo item at a time.", errorColor);
                }
                else
                {
                    taskInd--;

                    //check if item index exists in list
                    if (taskInd < 0 || taskInd >= tasks.Count)
                        print("Task no. " + taskInd + " doesn't exist in that todo file.", errorColor);
                    else
                    {
                        print("Deleted task: " + tasks[taskInd]);

                        //if target todolist is today, also delete from yesterday's list (otherwise the item will return)
                        if (date == "today")
                        {
                            List<string> prevTodo = tasklist(todoFile("previous"));

                            if (prevTodo.Contains(tasks[taskInd]))
                            {
                                prevTodo.Remove(tasks[taskInd]);

                                StreamWriter file = new StreamWriter(todoFile("previous"));
                                foreach (string item in prevTodo)
                                    file.WriteLine(item);
                                file.Close();
                            }
                        }

                        //delete from target todolist
                        tasks.RemoveAt(taskInd);
                    }
                }
            }
            else
            {
                //mark todo item as done
                //get indices of all items that need to be marked as done
                List<int> done = new List<int>();

                foreach (string tsk in task.Split(','))
                    if (tsk.Contains('-'))
                    {
                        //represents range
                        string[] bounds = tsk.Split('-');
                        for (int i = int.Parse(bounds[0]); i <= int.Parse(bounds[1]); i++)
                            done.Add(i);
                    }
                    else
                        //single task
                        done.Add(int.Parse(tsk));

                if (todoHideDone)
                    //ignore items that are already marked as done, as well as items that don't exist
                    for (int i = 0; i < done.Count; i++)
                    {
                        //get actual index of item (which means ignore done items)
                        int actualIndex = 0, j = 0;

                        while (true)
                        {
                            //skip done and erased items
                            while (actualIndex < tasks.Count && (tasks[actualIndex].Contains("__DONE__")))
                                actualIndex++;

                            if (actualIndex == tasks.Count)
                                break; //index out of bounds

                            if (j == done[i] - 1)
                                break; //loop exit condition

                            j++;
                            actualIndex++;
                        }

                        //check if item index exists in list
                        if (actualIndex < 0 || actualIndex >= tasks.Count)
                        {
                            print("Task no. " + done[i] + " doesn't exist in that todo file.", errorColor);
                            done.RemoveAt(i--);
                        }
                        else
                            done[i] = actualIndex;
                    }
                else
                    for (int i = 0; i < done.Count; i++)
                    {
                        done[i]--; //adjust item indices from 1-based to 0-based

                        //check if item indices exist in list
                        if (done[i] < 0 || done[i] >= tasks.Count)
                        {
                            print("Task no. " + (done[i] + 1) + " doesn't exist in that todo file.", errorColor);
                            done.RemoveAt(i--);
                        }

                        //also check if item already marked as done
                        if (tasks[done[i]].Contains("__DONE__"))
                        {
                            print("Task no. " + (done[i] + 1) + " is already marked as done.", errorColor);
                            done.RemoveAt(i--);
                        }
                    }

                if (done.Count > 0)
                {
                    done.Sort();

                    //mark items
                    foreach (int dn in done)
                    {
                        print(tasks[dn], true, todoDoneColor, true);
                        tasks[dn] = "__DONE__" + tasks[dn];
                    }
                }
            }

            //save updated list
            StreamWriter fWrtr = new System.IO.StreamWriter(todoFile(date));
            foreach (string tsk in tasks)
                fWrtr.WriteLine(tsk);
            fWrtr.Close();

            //reload today's todo list if needed
            if (date == "today")
                todoLoad();
        }
        #endregion

        #region Mail Module
        void mailInit()
        {
            if (!string.IsNullOrEmpty(mailUser) && !string.IsNullOrEmpty(mailPass))
            {
                gmail = new Gmail(mailUser, mailPass, finishedCheckingMail);
                getNewMailCount(MailCheckAction.MailInit);
            }
        }

        void getNewMailCount(MailCheckAction action)
        {
            gmail.Check(action);
        }

        void displayNewMailCount()
        {
            if (newMailCount == prevNewMailCount)
                return; //don't display mail count if it's already been displayed before

            string output;

            switch (newMailCount)
            {
                case -1:
                    print("Could not check for new emails.", errorColor);
                    print("Your Internet could be down, the mail server may be unresponsive, or the username and password that you entered previously were wrong..", errorColor);
                    output = "error";
                    break;
                case 0:
                    output = "No new mail.";
                    break;
                case 1:
                    output = "1 new email.";
                    break;
                default:
                    output = newMailCount + " new emails.";
                    break;
            }

            //check last outputted line to make sure it won't be repeated
            if (output != "error" && chunks.Count > 0 && chunks[chunks.Count - 1].ToString() != output)
                print(output, "https://mail.google.com/mail/ca/u/0/#inbox", mailCountColor);

            prevNewMailCount = newMailCount;

            //play sound notification
            if (mailSoundThreshold != 0 && newMailCount >= mailSoundThreshold && File.Exists(mailSound))
                PlaySound(mailSound, 0, SND_ASYNC);
        }

        void displayNewMail(bool verbose)
        {
            if (gmail == null || gmail.emails == null)
                return;

            getNewMailCount(MailCheckAction.NoAction); //temp line

            if (gmail.emails.Count == 0)
                print("No new mail.", "https://mail.google.com/mail/ca/u/0/#inbox", mailCountColor);
            else
            {
                print("Gmail - Inbox for " + mailUser, "https://mail.google.com/mail/ca/u/0/#inbox", mailHeaderColor);

                foreach (string[] email in gmail.emails)
                    if (!verbose)
                        print(email[gmail.M_SENDER] + ": " + email[gmail.M_TITLE] + " - " + email[gmail.M_DATE], email[gmail.M_LINK], mailHeaderColor);
                    else
                    {
                        print(email[gmail.M_SENDER] + ": " + email[gmail.M_TITLE] + " - " + email[gmail.M_DATE], email[gmail.M_LINK], mailHeaderColor);
                        print(email[gmail.M_SUMMARY], mailSummaryColor);
                    }
            }
        }

        void cmdMail(string[] cmd)
        {
            if (cmd.Length >= 4 && cmd[1].Contains("/s"))
            {
                //setup username/password
                mailUser = cmd[2];
                mailPass = cmd[3];

                saveOptions();

                print("Updated mail username/password.");
            }
            else if (string.IsNullOrEmpty(mailUser) || string.IsNullOrEmpty(mailPass))
            {
                print("You need to setup your mail account first.", errorColor);
                print("Use the following command: \"mail /setup [username] [password]\".", errorColor);
            }
            else if (cmd.Length >= 2 && cmd[1].Contains("/r"))
                //manual recheck
                getNewMailCount(MailCheckAction.ForceOutput);
            else
            {
                bool verbose = cmd.Length > 1 && cmd[1].Contains("/v");
                displayNewMail(verbose);
            }
        }

        void finishedCheckingMail(int mailCountResult, MailCheckAction action)
        {
            newMailCount = mailCountResult;

            //check if error
            if (newMailCount == -1)
                printError("Error while checking for new mail.", gmail.mailException);
            else
            {
                //perform designated action
                switch (action)
                {
                    case MailCheckAction.MailInit:
                        if (newMailCount != -1)
                        {
                            displayNewMailCount();
                            timerMailCheck.Enabled = true;
                        }
                        break;
                    case MailCheckAction.TimerCheck:
                        if (mailUpdateOnNewMail)
                            displayNewMailCount();
                        break;
                    case MailCheckAction.ForceOutput:
                        prevNewMailCount = -1; //force the mail count to display even if it's zero
                        displayNewMailCount();
                        break;
                }
            }
        }
        #endregion

        #region Twitter Module
        void twitterAuth()
        {
            print("You need to authorize Adjutant (console) before you can use it as a Twitter client.");
            print("The authorization URL has been launched in your browser.");
            print("Please click on \"Authorize app\" and then copy the given PIN into Adjutant.");

            if (twitter == null)
                twitter = new Twitter();

            Uri uri = twitter.GetAuthorizationUri();

            if (uri == null)
                printError("Error while getting Twitter authorization URI.", twitter.TwException);
            else
            {
                Process.Start(uri.ToString());

                inputMode = InputMode.TwitterPIN;
                setPrompt();
            }
        }

        void twitterInit()
        {
            if (token != "?" && secret != "?")
            {
                twitter = new Twitter(token, secret, lastTweet);

                Twitter.DisplayPictures = twDisplayPictures;
                Twitter.DisplayInstagrams = twDisplayInstagrams;
                twPrevCountBelowThreshold = true;

                if (!twitter.VerifyCredentials())
                {
                    print("Error while initializing Twitter service.<pause>", errorColor);
                    printHelp("To see more information about this error please type the following command: \"help twitter /init\"");
                    return;
                }

                if (twitter.GetNewTweets(lastTweet))
                    lastTweet = Math.Max(lastTweet, twitter.GetLastTweet());
                else
                    printError("Could not get a list of new tweets.", twitter.TwException);

                if (!twitter.EstablishStreamConnection())
                    printError("Could not establish a connection to Twitter's streaming API.", twitter.TwException);
            }
        }

        string howLongAgo(DateTime time)
        {
            TimeSpan diff = DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(time);

            if (diff.TotalSeconds < 1)
                return diff.Milliseconds + (diff.Milliseconds == 1 ? " msec ago" : " msecs ago");
            else if (diff.TotalMinutes < 1)
                return diff.Seconds + (diff.Seconds == 1 ? " sec ago" : " secs ago");
            else if (diff.TotalHours < 1)
                return diff.Minutes + (diff.Minutes == 1 ? " min ago" : " mins ago");
            else if (diff.TotalDays < 1)
                return diff.Hours + (diff.Hours == 1 ? " hr ago" : " hrs ago");
            else if (diff.TotalDays < 2)
                return "Yesterday";
            else
                return (int)diff.TotalDays + " days ago";
        }

        string getNextURL(string tweet, out int ind)
        {
            //match all URLs not preceded by <image=
            string match = Regex.Match(tweet, @"((?<!\<image\=)(http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)").ToString();
            //string match = Regex.Match(tweet, @"((http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)").ToString();

            if (match != "")
                ind = tweet.IndexOf(match);
            else
                ind = tweet.Length;

            return match;
        }

        string getNextMentionOrHashtag(string tweet, out int ind)
        {
            string match = Regex.Match(tweet, @"(?<!\w)[@#][\w_]+").ToString();

            if (match != "")
                ind = tweet.IndexOf(match);
            else
                ind = tweet.Length;

            return match;
        }

        void twitterPrint()
        {
            if (twitter.AnyNewTweets())
            {
                Twitter.Tweet newTweet = twitter.GetNextUnreadTweet();

                print(newTweet.user, "https://twitter.com/" + newTweet.actualUsername, false, twUserColor);
                print(" tweeted ", false, twMiscColor);

                twUsername = newTweet.actualUsername;

                //split tweet into chunks in order to color mentions and links
                string tweet = newTweet.text;
                twURLs.Clear();
                twMentions.Clear();

                int urlInd, mentionInd;

                string url = getNextURL(tweet, out urlInd);
                string mention = getNextMentionOrHashtag(tweet, out mentionInd);

                while (url != "" || mention != "")
                {
                    if (urlInd < mentionInd)
                    {
                        print(tweet.Substring(0, urlInd), false, twTweetColor);
                        print(url, url, false, twLinkColor);
                        twURLs.Add(url);

                        tweet = tweet.Remove(0, urlInd + url.Length);
                    }
                    else
                    {
                        print(tweet.Substring(0, mentionInd), false, twTweetColor);
                        print(mention, "https://twitter.com/" + mention.Replace("@", ""), false, twUserColor);
                        twMentions.Add(mention);

                        tweet = tweet.Remove(0, mentionInd + mention.Length);
                    }

                    url = getNextURL(tweet, out urlInd);
                    mention = getNextMentionOrHashtag(tweet, out mentionInd);
                }

                if (tweet != "")
                    print(tweet, false, twTweetColor);

                //print timestamp
                print(" " + howLongAgo(newTweet.created), "https://twitter.com/" + newTweet.actualUsername + "/status/" + newTweet.id, twTimeColor);

                lastTweet = newTweet.id;

                if (twitter.AnyUnreadTweets())
                    inputMode = InputMode.Twitter;
                else
                {
                    inputMode = InputMode.Default;

                    print("No more tweets.", "http://www.twitter.com/", twCountColor);
                    removeReadTweets();
                }
                
                setPrompt();
            }
        }

        void twitterPrint(bool all)
        {
            do
            {
                twitterPrint();
            } while (inputMode == InputMode.Twitter);
        }

        void removeReadTweets()
        {
            twitter.RemoveReadTweets();

            if (twitter.GetNewTweetCount() < twSoundThreshold)
                twPrevCountBelowThreshold = true;
        }

        void tweetCount()
        {
            int tweetCount = twitter.GetNewTweetCount();
            string tweetCountMsg;

            switch (tweetCount)
            {
                case 0:
                    tweetCountMsg = "No new tweets.";
                    break;
                case 1:
                    tweetCountMsg = "1 new tweet.";
                    break;
                default:
                    tweetCountMsg = tweetCount + " new tweets.";
                    break;
            }

            if ((chunks.Count == 0 || chunks[chunks.Count - 1].ToString() != tweetCountMsg) //don't display new tweet count if it's already displayed
                && inputMode != InputMode.Twitter) //or if Adjutant is currently in Twitter mode
                print(tweetCountMsg, "http://www.twitter.com/", twCountColor);

            //play sound notification
            if (twSoundThreshold != 0 && tweetCount >= twSoundThreshold && twPrevCountBelowThreshold && File.Exists(twSound))
            {
                PlaySound(twSound, 0, SND_ASYNC);
                twPrevCountBelowThreshold = false;
            }

            lastTwCount = DateTime.Now;
        }

        void cmdTwitter(string[] cmd)
        {
            if (twitter == null && (cmd.Length == 1 || !cmd[1].ToLower().Contains("/i")))
            {
                if (token == "?" || secret == "?")
                    twitterAuth();
                else
                {
                    print("Twitter service is not initialized.<pause>", errorColor);
                    printHelp("To see more information about this error please type the following command: \"help twitter /init\"");
                }
            }
            else
            {
                if (cmd.Length == 1)
                {
                    tweetCount();

                    if (twitter.AnyNewTweets())
                    {
                        //twInd = 0;
                        twitterPrint();
                    }
                }
                else if (cmd[1].ToLower().Contains("/a"))
                    twitterPrint(true);
                else if (cmd[1].ToLower().Contains("/i"))
                    twitterAuth();
            }
        }
        #endregion

        #region Reddit Module
        void redditInit()
        {
            reddit = new Reddit();
        }

        void cmdReddit(string[] cmd)
        {
            if (cmd.Length > 1)
            {
                //browse subreddit
                reddit.PreparePosts(cmd[1]);

                inputMode = InputMode.Reddit;
                setPrompt();
                redditPrint();
            }
        }

        void redditPrint()
        {
            lastPost = reddit.GetNextPost();

            print(lastPost.user, "http://www.reddit.com/u/" + lastPost.user, false, redditLinkColor);
            print(" submitted ", false, redditMiscColor);
            print((lastPost.score > 0 ? "[+" : "[") + lastPost.score + "]", false, redditMiscColor);
            print(lastPost.title, lastPost.url, false, redditLinkColor);
            print(" to ", false, redditMiscColor);
            print(lastPost.subreddit, "http://www.reddit.com" + lastPost.subreddit, false, redditLinkColor);
            print(" " + howLongAgo(lastPost.created), false, redditMiscColor);
            print(" (" + lastPost.nComments + " comments)", lastPost.permalink, redditLinkColor);

            //download image/thumbnail if any
            if (lastPost.image != "")
                print(lastPost.image, lastPost.url, redditLinkColor);
        }
        #endregion

        #region Pad Module
        void cmdPad(string[] cmd)
        {
            //first finish any remaining outputs (which may include the "pad" command echo)
            flush();
            jumpToLastLine();
            
            //then set Pad mode
            setOutputMode(OutputMode.Pad);
            
            //load file?
            if (cmd.Length > 1)
            {
                //remove any quotes from path
                if (cmd[1].Length > 0 && cmd[1][0] == '"')
                    cmd[1] = cmd[1].Substring(1);
                if (cmd[1].Length > 0 && cmd[1][cmd[1].Length - 1] == '"')
                    cmd[1] = cmd[1].Substring(0, cmd[1].Length - 1);

                //path missing directory?
                if (!File.Exists(cmd[1]) && File.Exists(dir + cmd[1]))
                    cmd[1] = dir + cmd[1];

                //try to open file
                if (!padOpenFile(cmd[1]))
                {
                    print("Specified file doesn't exist.", errorColor);
                    setOutputMode(OutputMode.Default);
                }
            }
        }

        void padNewFile()
        {
            padFilePath = "";
            padFileContents = "";
            sciPad.Text = "";
            padMenusFilename.Text = "Untitled";
        }

        bool padCloseFile()
        {
            if (sciPad.Text != padFileContents)
                switch (MessageBox.Show("Do you want to save changes to " + padMenusFilename.Text, "Adjutant_Pad", MessageBoxButtons.YesNoCancel, MessageBoxIcon.None))
                {
                    case DialogResult.Yes:
                        return padSaveFile(false); //close the file only if the user saves it
                    case DialogResult.No:
                        return true;
                    case DialogResult.Cancel:
                        return false;
                    default:
                        return false;
                }
            else
                return true;
        }

        bool padSaveFile(bool saveAs)
        {
            if (padFilePath == "" || saveAs)
            {
                //show save dialog
                saveDiagPad.ShowDialog();
                this.Opacity = opacityActive;

                if (saveDiagPad.FileName != "")
                    padFilePath = saveDiagPad.FileName;
                else
                    return false;
            }

            //save contents to file
            StreamWriter file = new StreamWriter(padFilePath);
            file.Write(sciPad.Text);
            file.Close();

            padSetSyntax();

            return true;
        }

        bool padOpenFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                padFilePath = filePath;

                StreamReader file = new StreamReader(padFilePath);
                sciPad.Text = file.ReadToEnd();
                file.Close();

                padFileContents = sciPad.Text;
                padMenusFilename.Text = Path.GetFileName(padFilePath);

                padSetSyntax();

                return true;
            }
            else
                return false;
        }

        void padSetSyntax()
        {
            //set syntax to current language
            switch (Path.GetExtension(padFilePath))
            {
                case ".py":
                    padMenusViewSyntaxPython.PerformClick();
                    break;
                default:
                    break;
            }

            padFileContents = sciPad.Text;
            padMenusFilename.Text = Path.GetFileName(padFilePath);
        }

        private void padMenusFileNew_Click(object sender, EventArgs e)
        {
            if (padCloseFile())
                padNewFile();
        }

        private void padMenusFileOpen_Click(object sender, EventArgs e)
        {
            if (padCloseFile())
            {
                if (File.Exists(padFilePath))
                    openDiagPad.InitialDirectory = Path.GetDirectoryName(padFilePath);

                openDiagPad.ShowDialog();
                this.Opacity = opacityActive;

                padOpenFile(openDiagPad.FileName);
            }
        }

        private void padMenusFileSave_Click(object sender, EventArgs e)
        {
            padSaveFile(false);
        }

        private void padMenusFileSaveAs_Click(object sender, EventArgs e)
        {
            padSaveFile(true);
        }

        private void padMenusFileExitPad_Click(object sender, EventArgs e)
        {
            if (padCloseFile())
                setOutputMode(OutputMode.Default);
        }

        private void padMenusViewWordWrap_Click(object sender, EventArgs e)
        {
            padMenusViewWordWrap.Checked = !padMenusViewWordWrap.Checked;

            if (padMenusViewWordWrap.Checked)
                sciPad.LineWrapping.Mode = LineWrappingMode.Word;
            else
                sciPad.LineWrapping.Mode = LineWrappingMode.None;

            saveOptions();
        }

        private void padMenusViewShowLineNumbers_Click(object sender, EventArgs e)
        {
            padMenusViewShowLineNumbers.Checked = !padMenusViewShowLineNumbers.Checked;

            if (padMenusViewShowLineNumbers.Checked)
                sciPad.Margins[0].Width = 20;
            else
                sciPad.Margins[0].Width = 0;

            saveOptions();
        }

        private void padMenusViewSyntax_SelectLanguage_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuSender = (ToolStripMenuItem)sender;

            //deactivate other languages
            padMenusViewSyntaxAssembly.Checked = false;
            padMenusViewSyntaxCS.Checked = false;
            padMenusViewSyntaxHTML.Checked = false;
            padMenusViewSyntaxJavaScript.Checked = false;
            padMenusViewSyntaxMSSQL.Checked = false;
            padMenusViewSyntaxPostgreSQL.Checked = false;
            padMenusViewSyntaxPython.Checked = false;
            padMenusViewSyntaxVBScript.Checked = false;
            padMenusViewSyntaxXML.Checked = false;

            //activate this language
            menuSender.Checked = true;

            bool monokai = false;

            switch (menuSender.Text)
            {
                case "Plain Text":
                    foreach (var style in sciPad.Lexing.StyleNameMap)
                    {
                        sciPad.Styles[style.Value].BackColor = sciPad.BackColor;
                        sciPad.Styles[style.Value].ForeColor = sciPad.ForeColor;
                    }
                    return;
                case "Assembly":
                    sciPad.ConfigurationManager.Language = "asm";
                    break;
                case "C#":
                    sciPad.ConfigurationManager.Language = "cs";
                    monokai = true;
                    break;
                case "HTML":
                    sciPad.ConfigurationManager.Language = "html";
                    break;
                case "JavaScript":
                    sciPad.ConfigurationManager.Language = "js";
                    break;
                case "MS SQL":
                    sciPad.ConfigurationManager.Language = "mssql";
                    break;
                case "Postgre SQL":
                    sciPad.ConfigurationManager.Language = "psql";
                    break;
                case "Python":
                    sciPad.ConfigurationManager.Language = "python";
                    monokai = true;
                    break;
                case "VB Script":
                    sciPad.ConfigurationManager.Language = "vbscript";
                    break;
                case "XML":
                    sciPad.ConfigurationManager.Language = "xml";
                    break;
            }

            //customize colors
            if (sciPad.Lexing.StyleNameMap.ContainsKey("LINENUMBER"))
            {
                sciPad.Styles[sciPad.Lexing.StyleNameMap["LINENUMBER"]].ForeColor = sciPad.ForeColor;
                sciPad.Styles[sciPad.Lexing.StyleNameMap["LINENUMBER"]].BackColor = sciPad.BackColor;
            }

            if (monokai)
            {
                //set Monokai color scheme
                sciPad.Styles[sciPad.Lexing.StyleNameMap["DOCUMENT_DEFAULT"]].ForeColor = sciPad.ForeColor;
                sciPad.Styles[sciPad.Lexing.StyleNameMap["NUMBER"]].ForeColor = Color.FromArgb(190, 132, 255);
                sciPad.Styles[sciPad.Lexing.StyleNameMap["WORD"]].ForeColor = Color.FromArgb(249, 38, 114);
                sciPad.Styles[sciPad.Lexing.StyleNameMap["WORD2"]].ForeColor = Color.FromArgb(102, 217, 239);
                sciPad.Styles[sciPad.Lexing.StyleNameMap["STRING"]].ForeColor = Color.FromArgb(230, 219, 116);
                sciPad.Styles[sciPad.Lexing.StyleNameMap["CHARACTER"]].ForeColor = Color.FromArgb(230, 219, 116);

                if (sciPad.Lexing.StyleNameMap.ContainsKey("PREPROCESSOR"))
                    sciPad.Styles[sciPad.Lexing.StyleNameMap["PREPROCESSOR"]].ForeColor = Color.FromArgb(190, 132, 255);

                sciPad.Styles[sciPad.Lexing.StyleNameMap["OPERATOR"]].ForeColor = Color.FromArgb(249, 38, 114);
                sciPad.Styles[sciPad.Lexing.StyleNameMap["IDENTIFIER"]].ForeColor = sciPad.ForeColor;

                if (sciPad.Lexing.StyleNameMap.ContainsKey("COMMENT"))
                    sciPad.Styles[sciPad.Lexing.StyleNameMap["COMMENT"]].ForeColor = Color.FromArgb(101, 121, 164);
                if (sciPad.Lexing.StyleNameMap.ContainsKey("COMMENTLINE"))
                    sciPad.Styles[sciPad.Lexing.StyleNameMap["COMMENTLINE"]].ForeColor = Color.FromArgb(101, 121, 164);
                if (sciPad.Lexing.StyleNameMap.ContainsKey("COMMENTBLOCK"))
                    sciPad.Styles[sciPad.Lexing.StyleNameMap["COMMENTBLOCK"]].ForeColor = Color.FromArgb(101, 121, 164);

                if (sciPad.Lexing.StyleNameMap.ContainsKey("GLOBALCLASS"))
                    sciPad.Styles[sciPad.Lexing.StyleNameMap["GLOBALCLASS"]].ForeColor = Color.FromArgb(166, 226, 46);
                if (sciPad.Lexing.StyleNameMap.ContainsKey("CLASSNAME"))
                    sciPad.Styles[sciPad.Lexing.StyleNameMap["CLASSNAME"]].ForeColor = Color.FromArgb(166, 226, 46);
                if (sciPad.Lexing.StyleNameMap.ContainsKey("DEFNAME"))
                    sciPad.Styles[sciPad.Lexing.StyleNameMap["DEFNAME"]].ForeColor = Color.FromArgb(166, 226, 46);
            }
        }

        private void padMenusRun_Click(object sender, EventArgs e)
        {
            if (padSaveFile(false))
            {
                //check if padLanguages contains valid entry for this file
                if (!padLanguages.ContainsKey(Path.GetExtension(padFilePath)))
                {
                    if (MessageBox.Show("Do you want to browse for the interpreter/compiler?", "Adjutant doesn't know how to run this file type", MessageBoxButtons.OKCancel, MessageBoxIcon.Error) == DialogResult.OK)
                    {
                        openDiagPad.ShowDialog();

                        if (File.Exists(openDiagPad.FileName))
                            padLanguages.Add(Path.GetExtension(padFilePath), openDiagPad.FileName);
                        else
                            return;
                    }
                    else
                        return;
                }

                //run
                runProcess(padFilePath);

                //show console
                sciPad.Visible = false;
            }
        }

        private void padMenusFilename_Click(object sender, EventArgs e)
        {
            if (File.Exists(padFilePath))
                Process.Start(padFilePath);
        }
        #endregion

        #region Launcher Module
        public void RescanDirs()
        {
            launcherIndex.Clear();

            foreach (string scanDir in launcherScanDirs.Split('/'))
                if (scanDir != "")
                {
                    string[] dirOptions = scanDir.Split('>');

                    if (Directory.Exists(dirOptions[0]) && dirOptions[1] != "")
                        foreach (string filter in dirOptions[1].Split(','))
                        {
                            string actualFilter = filter;
                            while (actualFilter.Length > 0 && actualFilter[0] == ' ')
                                actualFilter = actualFilter.Substring(1);

                            launcherIndex.AddRange(Directory.GetFiles(dirOptions[0], actualFilter, SearchOption.AllDirectories));
                        }
                }
            //u w0t m8
            if (options != null)
                options.lblLauncherIndexCount.Text = launcherIndex.Count + " files in index";
        }

        void showSuggestions()
        {
            if (string.IsNullOrWhiteSpace(txtCMD.Text))
                return;
            
            string search = txtCMD.Text.ToLower();
            //search = "calc";
            List<string> results = new List<string>();

            //make a copy of the index, so it can be modified during the search
            List<string> index = new List<string>();

            foreach (string item in launcherIndex)
                index.Add(item);

            //first find filenames with exact substring
            results = index.FindAll(i => i.ToLower().Contains(search));
            index.RemoveAll(i => i.ToLower().Contains(search)); //remove found results from index
            results = results.OrderBy(r => r.Length).ToList(); //sort results by number of extra chars

            if (results.Count < launcherMaxSuggestions)
            {
                //then find filenames that have all of the search string's letters, in order
                List<Tuple<string, int>> moreResults = new List<Tuple<string, int>>();

                for (int i = 0; i < index.Count; i++)
                {
                    string item = Path.GetFileName(index[i]).ToLower();
                    int charInd = 0, extraChars = 0;

                    for (int j = 0; j < item.Length && charInd < search.Length; j++)
                        if (item[j] == search[charInd])
                            charInd++;
                        else
                            extraChars++;

                    if (charInd == search.Length) //found matches
                    {
                        moreResults.Add(new Tuple<string, int>(index[i], extraChars));
                        index.RemoveAt(i);
                        i--;
                    }
                }

                //sort results by number of extra chars between matched letters
                results.AddRange(moreResults.OrderBy(r => r.Item2).Select(i => i.Item1));

                if (results.Count < launcherMaxSuggestions)
                {
                    //finally find filenames that have all of the search string's letters, in any order
                    moreResults = new List<Tuple<string, int>>();

                    for (int i = 0; i < index.Count; i++)
                    {
                        string item = Path.GetFileName(index[i]).ToLower();
                        int searchCharInd = 0, itemCharInd = 0, nextItemCharInd, nTurns = 0;
                        bool goingRight = true;

                        for (int j = 0; j < search.Length; j++)
                        {
                            if (goingRight)
                            {
                                nextItemCharInd = item.IndexOf(search[searchCharInd], itemCharInd);

                                if (nextItemCharInd == -1)
                                {
                                    //turn
                                    goingRight = false;
                                    nTurns++;
                                    nextItemCharInd = item.LastIndexOf(search[searchCharInd], itemCharInd);
                                }
                            }
                            else
                            {
                                nextItemCharInd = item.LastIndexOf(search[searchCharInd], itemCharInd);

                                if (nextItemCharInd == -1)
                                {
                                    //turn
                                    goingRight = true;
                                    nTurns++;
                                    nextItemCharInd = item.IndexOf(search[searchCharInd], itemCharInd);
                                }
                            }

                            if (nextItemCharInd != -1)
                            {
                                itemCharInd = nextItemCharInd;
                                item = item.Remove(itemCharInd, 1);

                                searchCharInd++;
                            }
                            else
                                break; //not a match
                        }

                        if (searchCharInd == search.Length) //found matches
                        {
                            moreResults.Add(new Tuple<string, int>(index[i], nTurns));
                            index.RemoveAt(i);
                            i--;
                        }
                    }

                    results.AddRange(moreResults.OrderBy(r => r.Item2).Select(i => i.Item1)); //sort results by number of turns
                }
            }

            //remove results if too many
            if (results.Count > launcherMaxSuggestions)
            {
                int n = results.Count - launcherMaxSuggestions;
                results.RemoveRange(results.Count - n, n);
            }
            
            if (launcherChunks.Count > 0)
            {
                //clear previous results
                foreach (Chunk chunk in launcherChunks)
                    chunk.DisposeResources();

                launcherChunks.Clear();
            }

            //print results
            foreach (string path in results)
            {
                //print icon
                launcherChunks.Add(new Chunk(Icon.ExtractAssociatedIcon(path).ToBitmap(), path));

                //print filename
                string filename = Path.GetFileName(path);
                launcherChunks.Add(new Chunk(filename, path, txtCMD.ForeColor, false, true, true, measureWidth(filename), lineH));
            }

            launcherSelection = 0;
            launcherPrevSearch = txtCMD.Text;
        }
        #endregion

        #region Weather Module
        void cmdWeather(string[] cmd)
        {
            if (cmd.Length == 1)
                showCurrentWeather();
            else
            {
                bool hourly = false;
                int nDays = 0;

                foreach (string par in cmd)
                    if (par.ToLower().Contains("/h"))
                        hourly = true;
                    else if (par.ToLower().Contains("/d") && par.Contains('='))
                        int.TryParse(par.Substring(par.IndexOf('=') + 1), out nDays);

                if (nDays <= 0)
                {
                    if (hourly)
                        nDays = 1;
                    else
                        nDays = 5;
                }
                else
                {
                    if (hourly)
                        nDays = Math.Min(nDays, 5);
                    else
                        nDays = Math.Min(nDays, 16);
                }

                print((hourly ? "Hourly weather" : "Weather") + " forecast for the next " + (nDays > 1 ? nDays + " days" : "24 hours") + " in " + weatherLocation + ":");
                foreach (var report in Weather.GetForecast(weatherLocation, weatherLang, weatherMetric, nDays, hourly))
                    printWeatherReport(report, true);
            }
        }

        void showCurrentWeather()
        {
            if (weatherWebcam != "")
            {
                string webcamImage = Weather.GetWebcamImage(weatherWebcam);

                if (webcamImage != "")
                    print("<image=" + webcamImage + ">");
                else
                    printError("Error while downloading webcam image.", Weather.Exception);
            }

            printWeatherReport(Weather.GetCurrentData(weatherLocation, weatherLang, weatherMetric), false);
        }

        void printWeatherReport(Weather.Report report, bool forecast)
        {
            string weatherURL = "";
            if (!forecast)
                weatherURL = "http://www.openweathermap.com/find?q=" + weatherLocation;

            //build header
            string header, body = "";
            if (!forecast)
                header = weatherLocation;
            else
                header = report.timestamp;

            //print weather icons
            for (int i = 0; i < report.descs.Length; i++)
            {
                if (i < report.icons.Length)
                    print("<image=" + Application.StartupPath + "\\ui\\weather icons\\" + report.icons[i] + ".png>", weatherURL, false, txtCMD.ForeColor);
                body += (body != "" ? " / " : "") + report.descs[i];
            }

            //build body
            if (report.temp != -1)
                body += (body != "" ? " / " : "") + Math.Round(report.temp, 1) + (weatherMetric ? " °C" : " °F");
            else
                body += (body != "" ? " / " : "") + Math.Round(report.tempMin, 1) + " — " + Math.Round(report.tempMax, 1) + (weatherMetric ? " °C" : " °F");
            if (report.wind != -1)
                body += (body != "" ? " / " : "") + report.windDesc + " (" + Math.Round(report.wind, 1) + (weatherMetric ? " m/s" : " mph");
            if (report.rain != -1)
                body += (body != "" ? " / " : "") + Math.Round(report.rain, 1) + " mm precipitation (" + report.rainPeriod + ")";

            body = body.Substring(0, 1).ToUpper() + body.Substring(1); //uppercase first letter
            
            //print weather header & body
            print(header, weatherURL, txtCMD.ForeColor);
            print(body);

            chunks[chunks.Count - 1].InsertDefinitiveNewline();
        }
        #endregion


        public formMain()
        {
            InitializeComponent();
        }

        protected override CreateParams CreateParams
        {
            //hide form from alt tab
            get
            {
                // Turn on WS_EX_TOOLWINDOW style bit
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            //activation hotkey
            if (m.Msg == Hotkey.WM_HOTKEY)
            {
                if ((int)m.WParam == this.GetHashCode())
                    activateConsole();
                else
                {
                    //launcher hotkey
                    if (hidden)
                        activateConsole();

                    inputMode = InputMode.Launcher;
                    setPrompt();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ImageAnimator.UpdateFrames();
            draw(grafx.Graphics);
            grafx.Render(e.Graphics);
        }

        private void OnFrameChanged(object o, EventArgs e)
        {
            this.Invalidate(); //force OnPaint
        }

        void ActivityHook_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.LWin)
                winKey = true;

            if (e.KeyData == Keys.D && winKey)
            {
                this.TopMost = true;
                timerDisableTopMost.Enabled = true;
            }
        }

        void ActivityHook_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.LWin)
                winKey = false;
        }

        void ActivityHook_OnMouseActivity(object sender, MouseEventArgs e)
        {
            if (!this.Visible)
                return;

            if (this.Bounds.Contains(e.Location))
            {
                this.Opacity = opacityActive;
                autohideTrigger();
            }
            else if (!txtCMD.Focused)
                    this.Opacity = opacityPassive;

            if (padResizingW || padResizingH)
            {
                if (e.Delta == Misc.MOUSE_UP_CODE)
                {
                    //user has stopped resizing
                    padResizingW = false;
                    padResizingH = false;

                    padW = this.Width;
                    padH = this.Height;

                    saveOptions();
                    initGrafx();
                    windowAutosize();
                }
                else
                {
                    if (padResizingW)
                        this.Width = e.X - this.Left;

                    if (padResizingH)
                        this.Height = e.Y - this.Top;
                }
            }
        }

        private void formMain_Load(object sender, EventArgs e)
        {
            //tray context menu
            trayIcon.ContextMenuStrip = contextMenu;

            //setup dialogs
            openDiagPad.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            saveDiagPad.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            //init buffer & gfx
            context = BufferedGraphicsManager.Current;

            initGrafx(); //initializes grafx to current window size (this call is required because loadOptions needs grafx to be initialized)

            //load options; init window & grafx
            loadOptions();
            initGrafx(); //initGrafx() is repeated here so the buffer size can be increased to max potential size (the value of which was loaded with other options)

            windowAutosize();

            loadCustomCmds();

            //setup Chunk delegates and init other stuff
            Chunk.MeasureWidth = measureWidth;
            Chunk.ForceConsoleResizeEvent = forceConsoleResizeEvent;
            Chunk.UpdateImage = updateImage;
            Chunk.SendChunkToNewLine = sendChunkToNewLine;
            Chunk.CheckIfBully = checkIfBully;
            Chunk.ExpandingChunks = expandingChunks;
            Chunk.OnFrameChangedEvent = new EventHandler(this.OnFrameChanged);
            Chunk.ErrorColor = errorColor;

            //calc Chunk wave colors
            int waveR = 0, waveG = 204, waveB = 0;
            int backR = this.BackColor.R, backG = this.BackColor.G, backB = this.BackColor.B;

            Chunk.WavePen = new Pen(Color.FromArgb(waveR, waveG, waveB), 2); //pure wave color
            Chunk.WavePen2 = new Pen(Color.FromArgb((int)((float)(waveR + backR * 2) / 3), (int)((float)(waveG + backG * 2) / 3), (int)((float)(waveB + backB * 2) / 3)), 3); //interpolate color between wave RGB and background RGB (much closer to background RGB)

            //load Chunk spinner
            if (File.Exists(Application.StartupPath + "\\ui\\loading.gif"))
            {
                Chunk.Spinner = Image.FromFile(Application.StartupPath + "\\ui\\loading.gif");

                if (ImageAnimator.CanAnimate(Chunk.Spinner))
                    ImageAnimator.Animate(Chunk.Spinner, new EventHandler(this.OnFrameChanged));
            }

            txtSelection.MouseWheel += new System.Windows.Forms.MouseEventHandler(txtPad_MouseWheel);

            link = "";
            helpColor = Color.Yellow;

            //get python exe path
            if (!padLanguages.ContainsKey(".py"))
            {
                RegistryKey pyKey = Registry.ClassesRoot.OpenSubKey(@"Python.File\shell\open\command");

                if (pyKey != null)
                {
                    string python = pyKey.GetValue("", "").ToString();

                    if (python[0] == '"' && python.IndexOf('"', 1) != -1)
                        python = python.Substring(1, python.IndexOf('"', 1) - 1);
                    else
                    {
                        python = python.Replace("%1", "").Replace("%*", "");
                        python = python.Replace("\"\"", "");
                        python = python.Replace("  ", " ");
                    }

                    padLanguages.Add(".py", python);
                }
            }

            //first run
            if (user == "first_run")
            {
                //create $path
                if (!customCmds.ContainsKey("$path"))
                    customCmds.Add("$path", Application.StartupPath);

                //display intro
                print("Welcome to Adjutant!<pause>");
                print("What is your name?");
                inputMode = InputMode.AwaitingUsername;
                setPrompt();

                return;
            }

            //print intro
            print("Adjutant online.");
            greeting();

            if (weatherShowOnStart)
                showCurrentWeather();

            todoLoad();
            RescanDirs(); //init launcher module

            //init net modules
            if (!RUN_WITHOUT_TWITTER_AND_GMAIL)
            {
                twitterInit();
                mailInit();
            }

            redditInit();
        }

        private void formMain_Activated(object sender, EventArgs e)
        {
            if (!initialized)
            {
                //global hotkey & mouse tracker
                //this is initialized here instead of in Form_Load because there it causes major mouse lag and general slowiness
                actHook = new UserActivityHook();
                actHook.KeyDown += new KeyEventHandler(ActivityHook_KeyDown);
                actHook.KeyUp += new KeyEventHandler(ActivityHook_KeyUp);
                actHook.OnMouseActivity += new MouseEventHandler(ActivityHook_OnMouseActivity);

                initialized = true;
            }

            txtCMD.Focus();

            if (twitter != null && twitter.AnyNewTweets() && twUpdateOnFocus && DateTime.Now.Subtract(lastTwCount).TotalSeconds >= minTweetPeriod)
                tweetCount();

            if (mailUpdateOnFocus && newMailCount > 0)
                displayNewMailCount();
        }

        private void formMain_Deactivate(object sender, EventArgs e)
        {
            this.Opacity = opacityPassive;
        }

        private void formMain_Resize(object sender, EventArgs e)
        {
            if (autoResize)
            {
                //if Adjutant resized itself do nothing here
                autoResize = false;
                return;
            }

            int lMarg = 0;

            //recalculate text chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                //can join with next chunk(s)?
                while (i < chunks.Count - 1 && chunks[i].JoinChunk(chunks[i + 1], lMarg))
                    chunks.RemoveAt(i + 1);

                //need to split chunk?
                List<Chunk> newChunks = Chunk.Chunkify(chunks[i].ToString(), chunks[i].GetLink(), chunks[i].GetColor(), chunks[i].GetStrikeout(), lMarg, true, chunks[i].IsAbsNewline());
                
                if (newChunks.Count > 1)
                {
                    if (lastChunk >= i) //if new chunks are inserted before lastChunk
                        lastChunk += newChunks.Count - 1; //change lastChunk accordingly

                    chunks.RemoveAt(i);

                    foreach (Chunk chunk in newChunks)
                        chunks.Insert(i++, chunk);

                    i--;

                    lMarg = 0;
                }

                if (chunks[i].IsNewline())
                    lMarg = 0;
                else
                    lMarg += chunks[i].GetWidth();
            }

            if (lastChunk >= chunks.Count)
            {
                lastChunk = chunks.Count - 1;

                if (lastChunk == -1)
                    lastChunkChar = 0;
                else
                    lastChunkChar = chunks[lastChunk].ToString().Length;
            }

            if (context != null)
            {
                windowAutosize();
                //jumpToLastLine();
                draw(grafx.Graphics);
                this.Refresh();
            }
        }

        private void formMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            saveOptions();
            Hotkey.UnregisterHotKey(this, false);
            Hotkey.UnregisterHotKey(this, true);
        }

        private void formMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Link;
            else
                e.Effect = DragDropEffects.None;
        }

        private void formMain_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                //concatenate all paths
                string paths = "";

                foreach (string path in (string[])(e.Data.GetData(DataFormats.FileDrop)))
                    paths += path + " ";

                //remove last space
                if (paths.Length > 0)
                    paths = paths.Substring(0, paths.Length - 1);

                //paste into txtCMD
                txtCMD.Text += paths;
            }
        }

        private void formMain_KeyDown(object sender, KeyEventArgs e)
        {
            ctrlKey = e.Control;

            switch (outputMode)
            {
                case OutputMode.Default:
                    if (e.KeyCode == Keys.F2)
                        setOutputMode(OutputMode.Selection);
                    break;
                case OutputMode.Selection:
                    if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Escape)
                        setOutputMode(OutputMode.Default);
                    else if (e.Control && e.KeyCode == Keys.A)
                        txtSelection.SelectAll();
                    break;
                case OutputMode.Pad:
                    if (e.KeyCode == Keys.Escape)
                    {
                        if (sciPad.Visible)
                            padMenusFileExitPad.PerformClick(); //exit Pad mode
                        else
                            sciPad.Visible = true; //return from console after running program
                    }
                    else if (e.Control && e.KeyCode == Keys.A)
                    {
                        txtSelection.SelectAll();
                        e.SuppressKeyPress = true;
                    }
                    break;
            }
        }

        private void formMain_KeyUp(object sender, KeyEventArgs e)
        {
            ctrlKey = e.Control;
        }

        private void formMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                mouseDown(e.X, e.Y);
        }

        private void formMain_MouseMove(object sender, MouseEventArgs e)
        {
            //mouse over text chunk with link?
            string newLink = "";
            int i;

            for (i = 0; i < chunks.Count; i++)
            {
                newLink = chunks[i].IfMouseOverReturnLink(e.Location);

                if (newLink != "")
                    break;
            }

            //let the other chunks know the mouse isn't over them (if they don't share the same link)
            //go back while chunks share the same link
            for (int j = i - 1; j >= 0; j--)
                if (!chunks[j].MouseNotOver(newLink))
                    break;

            //go forward while chunks share the same link
            for (int j = i + 1; j < chunks.Count; j++)
                if (!chunks[j].MouseNotOver(newLink))
                {
                    i = j;
                    break;
                }

            //go forward till the end
            for (i++; i < chunks.Count; i++)
                chunks[i].MouseNotOver("");

            //if new link update
            if (newLink != link)
            {
                link = newLink;

                draw(grafx.Graphics);
                this.Refresh();
            }

            //perform other mouse-movement logic
            mouseMove(e.X, e.Y);
        }

        private void formMain_MouseLeave(object sender, EventArgs e)
        {
            this.OnMouseMove(new MouseEventArgs(System.Windows.Forms.MouseButtons.None, 0, -1, -1, 0));
        }

        private void formMain_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                mouseUp(e.X, e.Y);

                //open link
                try
                {
                    if (link != "")
                        Process.Start(link);
                }
                catch (Exception exc)
                {
                    printError("Error while opening link.", exc);
                }
            }
        }

        private void formMain_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (chunks.Count == 0)
                return;

            //scroll up/down 3 lines
            int scrollLineH = 0, yToScroll = 3 * lineH, h;

            if (e.Delta < 0)
                while (yToScroll > 0)
                {
                    //get current chunk height
                    h = chunks[chunkOffset].GetHeight();

                    if (chunkOffset == chunks.Count - 1)
                    {
                        //no more chunks; check if last chunk is larger than the console
                        if (h > txtCMD.Top)
                            yOffset = Math.Max(yOffset - yToScroll, txtCMD.Top - h);
                        
                        break;
                    }

                    //is the chunk larger than the y amount left to scroll? (e.g. the chunk is a big image)
                    if (h > yToScroll)
                    {
                        if (yOffset - yToScroll > -h)
                        {
                            //don't skip drawing this chunk, just push it upwards
                            yOffset -= yToScroll;

                            //and move to the start of the current line
                            while (chunkOffset > 0 && !chunks[chunkOffset - 1].IsNewline())
                                chunkOffset--;
                            break;
                        }
                        else
                        {
                            //entire image chunk has been pushed upwards -> continue scrolling through chunks
                            scrollLineH = h - (-yOffset);
                            yOffset = 0;
                        }
                    }

                    //is this the last chunk in current line?
                    if (chunks[chunkOffset].IsNewline())
                    {
                        yToScroll -= scrollLineH;
                        scrollLineH = 0;
                    }

                    //make note of tallest chunk in current line
                    if (h > scrollLineH)
                        scrollLineH = h;

                    //move to next chunk
                    chunkOffset++;
                }
            else
            {
                //move to the first chunk in this line (it probably already is, but make sure)
                while (chunkOffset > 0 && !chunks[chunkOffset - 1].IsNewline())
                    chunkOffset--;

                while (yToScroll > 0)
                {
                    if (chunkOffset == 0)
                    {
                        //can't scroll past first chunk; check if it is larger than the console
                        if (yOffset == 0)
                        {
                            if (chunks[0].GetHeight() > txtCMD.Top)
                                yOffset = -chunks[0].GetHeight() + yToScroll;
                        }
                        else
                            yOffset = Math.Min(yOffset + yToScroll, 1); //yOffset will become max. 1 (instead of the more logical 0) because if yOffset becomes 0 it will scroll through the first chunk endlessly

                        break;
                    }

                    //scan through the previous line: find out the first chunk in the line and the max height
                    int tempChInd = chunkOffset;
                    scrollLineH = 0;

                    if (yOffset == 0)
                    {
                        do
                        {
                            tempChInd--;

                            if (chunks[tempChInd].GetHeight() > scrollLineH)
                                scrollLineH = chunks[tempChInd].GetHeight();
                        } while (tempChInd > 0 && !chunks[tempChInd - 1].IsNewline());
                    }
                    else
                    {
                        //image chunk is in the middle of being scrolled through
                        if (yOffset + yToScroll < 0)
                        {
                            //image chunk partly scrolled through
                            yOffset += yToScroll;
                            break;
                        }
                        else
                        {
                            //entire image chunk has been scrolled through
                            scrollLineH = -yOffset;
                            yOffset = 0;
                        }
                    }

                    //is the line larger than the y amount left to scroll? (e.g. the line contains a big image)
                    if (scrollLineH > yToScroll)
                        yOffset = -scrollLineH + yToScroll;

                    //move to start of previous line
                    chunkOffset = tempChInd;
                    yToScroll -= scrollLineH;
                }
            }

            draw(grafx.Graphics);
            this.Refresh();
        }

        private void formMain_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            setOutputMode(OutputMode.Selection);
        }

        private void txtPad_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!txtSelection.Text.Contains(Environment.NewLine))
                return; //no lines to scroll through

            int nLines = Math.Abs(e.Delta / 30); //number of lines to scroll
            int ind = txtSelection.SelectionStart;

            if (e.Delta < 0)
                for (int i = 0; i < nLines; i++)
                {
                    if (ind == txtSelection.Text.Length)
                        break;

                    ind = txtSelection.Text.IndexOf(Environment.NewLine, ind + 1);

                    if (ind == -1)
                    {
                        ind = txtSelection.Text.Length;
                        break;
                    }
                }
            else
                for (int i = 0; i < nLines; i++)
                {
                    if (ind == 0)
                        break;

                    ind = txtSelection.Text.LastIndexOf(Environment.NewLine, ind - 1);

                    if (ind == -1)
                    {
                        ind = 0;
                        break;
                    }
                }

            txtSelection.SelectionStart = ind;
            txtSelection.ScrollToCaret();
        }

        private void txtPad_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (outputMode == OutputMode.Selection)
                setOutputMode(OutputMode.Default);
        }

        private void UIElement_MouseDown(object sender, MouseEventArgs e)
        {
            Control control = (Control)sender;
            
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                mouseDown(control.Left + e.X, control.Top + e.Y);
        }

        private void UIElement_MouseMove(object sender, MouseEventArgs e)
        {
            Control control = (Control)sender;

            mouseMove(control.Left + e.X, control.Top + e.Y);
        }

        private void UIElement_MouseUp(object sender, MouseEventArgs e)
        {
            Control control = (Control)sender;

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                mouseUp(control.Left + e.X, control.Top + e.Y);
        }

        private void txtCMD_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            //prepare autoinactivate/autohide
            timerShowHide.Enabled = false;
            this.Opacity = opacityActive;

            autohideTrigger();

            switch (inputMode)
            {
                case InputMode.Twitter:
                    switch (e.KeyCode)
                    {
                        case Keys.Enter:
                            flush();
                            break;
                        case Keys.K:
                            flush();
                            twitter.GoToPreviousTweet();
                            twitterPrint();
                            break;
                        case Keys.J:
                            flush();
                            twitterPrint();
                            break;
                        case Keys.A:
                            twitterPrint(true);
                            break;
                        case Keys.O:
                            foreach (string twURL in twURLs)
                                Process.Start(twURL);

                            if (txtCMD.Text.ToLower() == "o")
                                txtCMD.Text = "";
                            break;
                        case Keys.M:
                            foreach (string twMention in twMentions)
                            {
                                string user = twMention;
                                if (user.Length > 1 && user[0] == '@')
                                    user = user.Substring(1);

                                Process.Start("https://www.twitter.com/" + user);
                            }

                            if (txtCMD.Text.ToLower() == "m")
                                txtCMD.Text = "";
                            break;
                        case Keys.U:
                            Process.Start("https://www.twitter.com/" + twUsername);

                            if (txtCMD.Text.ToLower() == "u")
                                txtCMD.Text = "";
                            break;
                        case Keys.T:
                            Process.Start("https://www.twitter.com/" + twUsername + "/status/" + lastTweet);

                            if (txtCMD.Text.ToLower() == "t")
                                txtCMD.Text = "";
                            break;
                        case Keys.Escape:
                            inputMode = InputMode.Default;
                            setPrompt();

                            twitter.RemoveReadTweets();
                            tweetCount();
                            break;
                    }
                    break;
                case InputMode.Reddit:
                    switch (e.KeyCode)
                    {
                        case Keys.Enter:
                            flush();
                            break;
                        case Keys.K:
                            flush();
                            reddit.GoToPreviousPost();
                            redditPrint();
                            break;
                        case Keys.J:
                            flush();
                            redditPrint();
                            break;
                        case Keys.Up:
                            //upvote todo
                            break;
                        case Keys.Down:
                            //downvote todo
                            break;
                        case Keys.U:
                            Process.Start("http://www.reddit.com/u/" + lastPost.user);

                            if (txtCMD.Text.ToLower() == "u")
                                txtCMD.Text = "";
                            break;
                        case Keys.R:
                            Process.Start("http://www.reddit.com/r/" + lastPost.subreddit);

                            if (txtCMD.Text.ToLower() == "r")
                                txtCMD.Text = "";
                            break;
                        case Keys.L:
                            Process.Start(lastPost.url);

                            if (txtCMD.Text.ToLower() == "l")
                                txtCMD.Text = "";
                            break;
                        case Keys.C:
                            Process.Start(lastPost.permalink);

                            if (txtCMD.Text.ToLower() == "c")
                                txtCMD.Text = "";
                            break;
                        case Keys.Escape:
                            inputMode = InputMode.Default;
                            setPrompt();

                            //twitter.RemoveReadTweets();
                            //tweetCount();
                            break;
                    }
                    break;
                case InputMode.Launcher:
                    e.SuppressKeyPress = false;
                    break;
                default:
                    switch (e.KeyCode)
                    {
                        case Keys.Enter:
                            command();
                            break;
                        case Keys.Back:
                            if (e.Control)
                            {
                                int ub = txtCMD.SelectionStart;

                                if (ub > 0)
                                {
                                    //delete previous word
                                    int lb = ub;

                                    while (lb > 0 && !char.IsLetterOrDigit(txtCMD.Text[lb - 1]))
                                        lb--;
                                    while (lb > 0 && char.IsLetterOrDigit(txtCMD.Text[lb - 1]))
                                        lb--;

                                    txtCMD.Text = txtCMD.Text.Remove(lb, ub - lb);
                                    txtCMD.SelectionStart = lb;
                                }
                            }
                            else
                            {
                                e.Handled = false;
                                e.SuppressKeyPress = false;
                            }
                            break;
                        case Keys.Tab:
                            string cmd = "", path = txtCMD.Text;

                            if (path.Contains('"'))
                            {
                                cmd = path.Substring(0, path.IndexOf('"'));
                                path = path.Substring(path.IndexOf('"') + 1);

                                if (path.Contains('"'))
                                    path = path.Substring(0, path.IndexOf('"'));
                            }
                            else if (path.Contains(' '))
                            {
                                cmd = path.Substring(0, path.LastIndexOf(' ') + 1);
                                path = path.Substring(path.LastIndexOf(' ') + 1);
                            }

                            setCMD(cmd + getFilteredPaths(path, e.Shift));
                            break;
                        case Keys.Up:
                            if (history.Count > 0)
                            {
                                historyInd--;
                                if (historyInd == -1)
                                    historyInd = history.Count - 1;

                                setCMD(history[historyInd]);
                            }
                            break;
                        case Keys.Down:
                            if (history.Count > 0)
                            {
                                historyInd++;
                                if (historyInd == history.Count)
                                    historyInd = 0;

                                setCMD(history[historyInd]);
                            }
                            break;
                        default:
                            e.Handled = false;
                            e.SuppressKeyPress = false;
                            break;
                    }
                    break;
            }
        }

        private void txtCMD_KeyUp(object sender, KeyEventArgs e)
        {
            //this code is here instead of in txtCMD_KeyDown because it needs to run after txtCMD.Text has received the new key
            if (inputMode == InputMode.Launcher)
                switch (e.KeyCode)
                {
                    case Keys.Up:
                        if (launcherSelection > 0)
                            launcherSelection--;
                        break;
                    case Keys.Down:
                        if (launcherSelection < launcherChunks.Count / 2 - 1)
                            launcherSelection++;
                        break;
                    case Keys.Enter:
                        //run file
                        string path = launcherChunks[launcherSelection * 2 + 1].GetLink();
                        Process.Start(path);

                        //exit Launcher mode
                        inputMode = InputMode.Default;
                        setPrompt();

                        if (launcherAutohide)
                            activateConsole();
                        break;
                    case Keys.Escape:
                        inputMode = InputMode.Default;
                        setPrompt();
                        break;
                    default:
                        if (txtCMD.Text != launcherPrevSearch)
                            showSuggestions();
                        break;
                }
        }

        private void sciPad_KeyDown(object sender, KeyEventArgs e)
        {
            this.Opacity = opacityActive;
        }

        private void timerDisableTopMost_Tick(object sender, EventArgs e)
        {
            this.TopMost = false;
            timerDisableTopMost.Enabled = false;
        }

        private void timerPrint_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now < pauseEnd)
                return; //paused

            update();

            //check if currently printing image
            if (chunks[lastChunk].IsImgExpanding())
                //don't advance to next chunk until this image chunk stops expanding
                return;

            //advance to next char/chunk
            lastChunkChar += printAtOnce;

            if (lastChunkChar >= chunks[lastChunk].ToString().Length + 1)
            {
                if (lastChunk == chunks.Count - 1)
                {
                    lastChunkChar = chunks[lastChunk].ToString().Length;
                    timerPrint.Enabled = false;

                    update();
                }
                else
                {
                    lastChunk++;
                    lastChunkChar = 0;

                    if (chunks[lastChunk].ToString() == "<pause>")
                    {
                        if (chunks[lastChunk].IsNewline() && lastChunk > 0 && !chunks[lastChunk - 1].IsNewline())
                            chunks[lastChunk - 1].InsertNewline(); //a newline needs to be inserted

                        update();
                        pauseEnd = DateTime.Now.AddMilliseconds(750);
                        chunks.RemoveAt(lastChunk);

                        if (lastChunk == chunks.Count)
                        {
                            //no more output
                            lastChunk--;
                            lastChunkChar = chunks[lastChunkChar].ToString().Length;
                            timerPrint.Enabled = false;
                        }
                    }
                }
            }
        }

        private void timerAutohide_Tick(object sender, EventArgs e)
        {
            if (this.Opacity == opacityPassive && DateTime.Now > autoHide)
            {
                timerAutohide.Enabled = false;

                if (hideStyle == HideStyle.Disappear)
                    this.Visible = false;
                else
                {
                    hiding = true;
                    timerShowHide.Enabled = true;
                }
            }
        }

        private void timerShowHide_Tick(object sender, EventArgs e)
        {
            switch (hideStyle)
            {
                case HideStyle.Fade:
                    if (hiding)
                    {
                        this.Opacity -= 0.05;
                        if (this.Opacity <= 0)
                        {
                            this.Opacity = 0;
                            timerShowHide.Enabled = false;
                            this.Visible = false;
                            hidden = true;
                        }
                    }
                    else
                    {
                        this.Opacity += 0.05;
                        if (this.Opacity >= opacityActive)
                        {
                            this.Opacity = opacityActive;
                            timerShowHide.Enabled = false;
                            hidden = false;
                        }
                    }
                    break;
                case HideStyle.ScrollUp:
                    if (hiding)
                    {
                        this.Top -= 25;
                        if (this.Top <= -this.Height)
                        {
                            this.Top = -this.Height + 1;
                            timerShowHide.Enabled = false;
                            //this.Visible = false;
                            hidden = true;
                        }
                    }
                    else
                    {
                        this.Top += 25;
                        if (this.Top >= y)
                        {
                            this.Top = y;
                            this.Opacity = opacityActive;
                            timerShowHide.Enabled = false;
                            hidden = false;
                        }
                    }
                    break;
                case HideStyle.ScrollDown:
                    if (hiding)
                    {
                        this.Top += 25;
                        if (this.Top >= Screen.PrimaryScreen.WorkingArea.Height)
                        {
                            timerShowHide.Enabled = false;
                            //this.Visible = false;
                            hidden = true;
                        }
                    }
                    else
                    {
                        this.Top -= 25;
                        if (this.Top <= y)
                        {
                            this.Top = y;
                            this.Opacity = opacityActive;
                            timerShowHide.Enabled = false;
                            hidden = false;
                        }
                    }
                    break;
                case HideStyle.ScrollLeft:
                    if (hiding)
                    {
                        this.Left -= 25;
                        if (this.Left <= -this.Width)
                        {
                            timerShowHide.Enabled = false;
                            //this.Visible = false;
                            hidden = true;
                        }
                    }
                    else
                    {
                        this.Left += 25;
                        if (this.Left >= x)
                        {
                            this.Left = x;
                            this.Opacity = opacityActive;
                            timerShowHide.Enabled = false;
                            hidden = false;
                        }
                    }
                    break;
                case HideStyle.ScrollRight:
                    if (hiding)
                    {
                        this.Left += 25;
                        if (this.Left >= Screen.PrimaryScreen.WorkingArea.Width)
                        {
                            timerShowHide.Enabled = false;
                            //this.Visible = false;
                            hidden = true;
                        }
                    }
                    else
                    {
                        this.Left -= 25;
                        if (this.Left <= x)
                        {
                            this.Left = x;
                            this.Opacity = opacityActive;
                            timerShowHide.Enabled = false;
                            hidden = false;
                        }
                    }
                    break;
            }
        }

        private void timerResizeWindow_Tick(object sender, EventArgs e)
        {
            int targetW, targetH;

            switch (outputMode)
            {
                default:
                case OutputMode.Default:
                    targetW = consoleW;
                    targetH = consoleH;
                    break;
                case OutputMode.Pad:
                    targetW = padW;
                    targetH = padH;
                    break;
            }

            //resize console by step (20)
            this.Width += Math.Sign((targetW - this.Width)) * 20;
            this.Height += Math.Sign((targetH - this.Height)) * 20;

            //reached final width or height?
            if (Math.Abs(targetW - this.Width) <= 10)
                this.Width = targetW;
            if (Math.Abs(targetH - this.Height) <= 10)
                this.Height = targetH;

            //job's done?
            if (this.Width == targetW && this.Height == targetH)
            {
                if (outputMode == OutputMode.Default)
                    jumpToLastLine();

                timerResizeWindow.Enabled = false;
            }
        }

        private void timerMailCheck_Tick(object sender, EventArgs e)
        {
            getNewMailCount(MailCheckAction.TimerCheck);
        }

        private void menuOptions_Click(object sender, EventArgs e)
        {
            showOptions();
        }

        private void menuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void trayIcon_DoubleClick(object sender, EventArgs e)
        {
            activateConsole();
        }
    }
}