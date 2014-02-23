﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Net;
using TweetSharp;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Adjutant
{
    public partial class formMain : Form
    {
        #region Declarations
        const string VERSION = "0.7";
        const string YEAR = "2014";

        const int ZERO_DELAY = 60001;


        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);


        public delegate void MyDelegate();

        struct Tweet
        {
            public string user, actualUsername, text, url;
            public DateTime created;
            public long id;

            public Tweet(string user, string actualUsername, string text, DateTime created, long id)
            {
                this.user = user;
                this.actualUsername = actualUsername;
                this.text = text.Replace("\n", Environment.NewLine);
                this.created = created;
                this.id = id;

                url = "https://twitter.com/" + user + "/status/" + id;
            }
        }

        class TextChunk
        {
            string text, link;
            Rectangle bounds;
            SolidBrush brush;
            bool newline, absNewline, mouseOver;

            public TextChunk(string text, string link, Color color, bool newline, bool absNewline, int w, int h)
            {
                this.text = text;
                this.link = link;
                brush = new SolidBrush(color);
                bounds = new Rectangle(0, 0, w, h);
                this.newline = newline;
                this.absNewline = absNewline;
            }

            public override string ToString()
            {
                return text;
            }

            public bool IsNewline()
            {
                return newline;
            }

            public bool IsAbsNewline()
            {
                return absNewline;
            }

            public Color GetColor()
            {
                return brush.Color;
            }

            public string GetLink()
            {
                return link;
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
                lastChar = Math.Min(text.Length, lastChar);

                bounds.X = x;
                bounds.Y = y;

                if (mouseOver)
                    gfx.DrawRectangle(new Pen(brush), bounds);

                gfx.DrawString(text.Substring(0, lastChar), font, brush, x, y);

                if (newline)
                {
                    x = 0;
                    y += bounds.Height;
                }
                else
                    x += bounds.Width;
            }

            public static List<TextChunk> Chunkify(string text, string link, Color color, int leftMargin, int maxWidth, Font font, bool newline, bool absNewline, Func<string, int> MeasureWidth, int lineH)
            {
                List<TextChunk> chunks = new List<TextChunk>();

                //if (text.Length <= 1)
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

                    chunks.Add(new TextChunk(text.Substring(0, len), link, color, text.Length != len || newline, absNewline && text.Length == len, segmentW, lineH));
                    text = text.Substring(len);

                    if (len < text.Length)
                        leftMargin = 0;
                }

                return chunks;
            }

            public bool JoinChunk(TextChunk nextChunk, int maxWidth, Font font, Func<string, int> MeasureWidth)
            {
                int newWidth = MeasureWidth(text + nextChunk.text);

                if (!absNewline && newWidth <= maxWidth && brush.Color == nextChunk.brush.Color)
                {
                    text += nextChunk.text;
                    bounds.Width = newWidth;
                    newline |= nextChunk.newline;
                    absNewline |= nextChunk.absNewline;

                    return true;
                }
                else
                    return false;
            }
        }

        enum HideStyle { Disappear, Fade, ScrollUp, ScrollDown, ScrollLeft, ScrollRight };

        HideStyle hideStyle;
        Gmail gmail;
        TwitterService twitter;
        OAuthRequestToken requestToken;
        UserActivityHook actHook;
        formOptions options;
        Process proc;
        BufferedGraphicsContext context;
        BufferedGraphics grafx;
        Brush brush = Brushes.White;
        Color echoColor, errorColor, todoMiscColor, todoItemColor, todoDoneColor, twUserColor, twMiscColor, twTweetColor, twLinkColor, twTimeColor, twCountColor, mailCountColor, mailHeaderColor, mailSummaryColor;
        DateTime autoHide, pauseEnd, lastTwCount;
        List<Tweet> tweets = new List<Tweet>();
        List<TextChunk> chunks = new List<TextChunk>();
        Dictionary<string, string> customCmds;
        List<string> history = new List<string>(), twURLs = new List<string>(), todo;
        string[] filteredPaths;
        string python, user, dir, todoDir, inputMode, token, secret, link, mailUser, mailPass;
        double opacityPassive, opacityActive;
        long lastTweet;
        int x, y, lineH, minH, maxH, prevH, maxLines, prevX, prevY, leftMargin, chunkOffset, lastChunk, lastChunkChar, printAtOnce, autoHideDelay, tabInd, historyInd, minTweetPeriod, hotkey, newMailCount, prevNewMailCount;
        bool initialized, activated, winKey, prompt, blankLine, echo, ctrlKey, drag, resizeW, resizeH, hiding, hidden, todoHideDone, todoAutoTransfer, twUpdateOnNewTweet, twUpdateOnFocus, twitterOutput, mailUpdateOnNewMail, mailUpdateOnFocus, hotkeyCtrl, hotkeyAlt, hotkeyShift;
        #endregion


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
                else if (!hidden && !txtCMD.Focused)
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

        void draw(Graphics gfx)
        {
            gfx.Clear(this.BackColor);

            if (chunks.Count > 0)
            {
                int x = 0, y = 0;
                int chInd = chunkOffset;

                while (chInd < lastChunk && y < txtCMD.Top)
                    chunks[chInd++].Draw(gfx, txtCMD.Font, ref x, ref y);
                chunks[lastChunk].Draw(gfx, txtCMD.Font, ref x, ref y, lastChunkChar);
            }
        }

        void MeasureLineHeight()
        {
            lineH = (int)grafx.Graphics.MeasureString("A", txtCMD.Font).Height;
        }

        int MeasureWidth(string txt)
        {
            return (int)grafx.Graphics.MeasureString(txt, txtCMD.Font, int.MaxValue, new StringFormat(StringFormatFlags.MeasureTrailingSpaces)).Width;
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
                            Hotkey.RegisterHotKey(this, hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift);
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
                            Color backColor = Color.FromArgb(int.Parse(args[1]));
                            this.BackColor = backColor;
                            lblPrompt.BackColor = backColor;
                            txtCMD.BackColor = backColor;
                            break;
                        case "text_color":
                            Color txtColor = Color.FromArgb(int.Parse(args[1]));
                            lblPrompt.ForeColor = txtColor;
                            txtCMD.ForeColor = txtColor;
                            break;
                        case "text_font":
                            lblPrompt.Font = new Font(args[1], lblPrompt.Font.Size);
                            txtCMD.Font = new Font(args[1], txtCMD.Font.Size);
                            break;
                        case "text_size":
                            lblPrompt.Font = new Font(lblPrompt.Font.Name, int.Parse(args[1]));
                            txtCMD.Font = new Font(txtCMD.Font.Name, int.Parse(args[1]));
                            break;
                        case "text_bold":
                            if (bool.Parse(args[1]))
                            {
                                FontStyle style;
                                if (txtCMD.Font.Italic)
                                    style = FontStyle.Bold | FontStyle.Italic;
                                else
                                    style = FontStyle.Bold;

                                lblPrompt.Font = new Font(lblPrompt.Font, style);
                                txtCMD.Font = new Font(txtCMD.Font, style);
                            }
                            break;
                        case "text_italic":
                            if (bool.Parse(args[1]))
                            {
                                FontStyle style;
                                if (txtCMD.Font.Bold)
                                    style = FontStyle.Bold | FontStyle.Italic;
                                else
                                    style = FontStyle.Italic;

                                lblPrompt.Font = new Font(lblPrompt.Font, style);
                                txtCMD.Font = new Font(txtCMD.Font, style);
                            }
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
                            setPrompt();
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
                        case "mail_period":
                            timerMailCheck.Interval = int.Parse(args[1]) * 60000; //convert from minutes to miliseconds
                            break;
                        case "mail_update_on_new_mail":
                            mailUpdateOnNewMail = bool.Parse(args[1]);
                            break;
                        case "mail_update_on_focus":
                            mailUpdateOnFocus = bool.Parse(args[1]);
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
                        case "user":
                            user = args[1];
                            break;
                    }
                }
            }

            file.Close();

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
        }

        void saveOptions()
        {
            StreamWriter file = new System.IO.StreamWriter(Application.StartupPath + "\\options.txt");

            file.WriteLine("//window");
            file.WriteLine("x=" + x);
            file.WriteLine("y=" + y);
            file.WriteLine("width=" + this.Width);
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

            file.WriteLine("//todo module");
            file.WriteLine("todo_dir=" + todoDir);
            file.WriteLine("todo_hide_done=" + todoHideDone);
            file.WriteLine("todo_auto_transfer=" + todoAutoTransfer);
            file.WriteLine("todo_misc_color=" + todoMiscColor.ToArgb());
            file.WriteLine("todo_item_color=" + todoItemColor.ToArgb());
            file.WriteLine("todo_done_color=" + todoDoneColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//twitter module");
            file.WriteLine("token=" + token);
            file.WriteLine("secret=" + secret);
            file.WriteLine("last_tweet=" + lastTweet);
            file.WriteLine("update_on_new_tweet=" + twUpdateOnNewTweet);
            file.WriteLine("update_on_focus=" + twUpdateOnFocus);
            file.WriteLine("min_tweet_period=" + minTweetPeriod);
            file.WriteLine("tw_user_color=" + twUserColor.ToArgb());
            file.WriteLine("tw_misc_color=" + twMiscColor.ToArgb());
            file.WriteLine("tw_tweet_color=" + twTweetColor.ToArgb());
            file.WriteLine("tw_link_color=" + twLinkColor.ToArgb());
            file.WriteLine("tw_time_color=" + twTimeColor.ToArgb());
            file.WriteLine("tw_count_color=" + twCountColor.ToArgb());
            file.WriteLine();

            file.WriteLine("//mail");
            file.WriteLine("mail_period=" + (timerMailCheck.Interval / 60000)); //convert from miliseconds to minutes
            file.WriteLine("mail_update_on_new_mail=" + mailUpdateOnNewMail);
            file.WriteLine("mail_update_on_focus=" + mailUpdateOnFocus);
            file.WriteLine("mail_count_color=" + mailCountColor.ToArgb());
            file.WriteLine("mail_header_color=" + mailHeaderColor.ToArgb());
            file.WriteLine("mail_summary_color=" + mailSummaryColor.ToArgb());
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

                options.main = this;
                options.starting = true;

                //load font list
                options.comboFont.Items.Clear();
                foreach (FontFamily font in System.Drawing.FontFamily.Families)
                    options.comboFont.Items.Add(font.Name);

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
                options.checkTodoHideDone.Tag = todoHideDone;
                options.checkTodoAutoTransfer.Tag = todoAutoTransfer;
                options.picTodoMiscColor.Tag = todoMiscColor;
                options.picTodoItemColor.Tag = todoItemColor;
                options.picTodoDoneColor.Tag = todoDoneColor;

                options.checkTwCountOnNewTweet.Tag = twUpdateOnNewTweet;
                options.checkTwCountOnFocus.Tag = twUpdateOnFocus;
                options.numTwCountMinPeriod.Tag = minTweetPeriod;
                options.picTwUsernameColor.Tag = twUserColor;
                options.picTwMiscColor.Tag = twMiscColor;
                options.picTwTweetColor.Tag = twTweetColor;
                options.picTwLinkColor.Tag = twLinkColor;
                options.picTwTimestampColor.Tag = twTimeColor;
                options.picTwCountColor.Tag = twCountColor;

                options.txtUser.Tag = mailUser;
                options.txtPass.Tag = mailPass;
                options.checkMailCountOnNewMail.Tag = mailUpdateOnNewMail;
                options.checkMailCountOnFocus.Tag = mailUpdateOnFocus;
                options.numMailCheckPeriod.Tag = timerMailCheck.Interval / 60000;
                options.picMailCountColor.Tag = mailCountColor;
                options.picMailHeaderColor.Tag = mailHeaderColor;
                options.picMailSummaryColor.Tag = mailSummaryColor;

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
                options.checkTodoHideDone.Checked = todoHideDone;
                options.checkTodoAutoTransfer.Checked = todoAutoTransfer;
                options.picTodoMiscColor.BackColor = todoMiscColor;
                options.picTodoItemColor.BackColor = todoItemColor;
                options.picTodoDoneColor.BackColor = todoDoneColor;

                options.checkTwCountOnNewTweet.Checked = twUpdateOnNewTweet;
                options.checkTwCountOnFocus.Checked = twUpdateOnFocus;
                options.numTwCountMinPeriod.Value = minTweetPeriod;
                options.picTwUsernameColor.BackColor = twUserColor;
                options.picTwMiscColor.BackColor = twMiscColor;
                options.picTwTweetColor.BackColor = twTweetColor;
                options.picTwLinkColor.BackColor = twLinkColor;
                options.picTwTimestampColor.BackColor = twTimeColor;
                options.picTwCountColor.BackColor = twCountColor;

                options.txtUser.Text = mailUser;
                options.txtPass.Text = mailPass;
                options.checkMailCountOnNewMail.Checked = mailUpdateOnNewMail;
                options.checkMailCountOnFocus.Checked = mailUpdateOnFocus;
                options.numMailCheckPeriod.Value = timerMailCheck.Interval / 60000;
                options.picMailCountColor.BackColor = mailCountColor;
                options.picMailHeaderColor.BackColor = mailHeaderColor;
                options.picMailSummaryColor.BackColor = mailSummaryColor;

                options.starting = false;

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

            this.BackColor = options.picBackColor.BackColor;
            txtCMD.BackColor = options.picBackColor.BackColor;

            FontStyle style = FontStyle.Regular;
            if (options.chkBold.Checked && options.chkItalic.Checked)
                style = FontStyle.Bold | FontStyle.Italic;
            else if (options.chkBold.Checked)
                style = FontStyle.Bold;
            else if (options.chkItalic.Checked)
                style = FontStyle.Italic;

            lblPrompt.Font = new Font(options.comboFont.Text, (float)options.numFontSize.Value, style);
            txtCMD.Font = new Font(options.comboFont.Text, (float)options.numFontSize.Value, style);
            lblPrompt.ForeColor = options.picTextColor.BackColor;
            txtCMD.ForeColor = options.picTextColor.BackColor;

            dir = options.txtStartDir.Text;
            setDelay(timerPrint, (int)options.numPrintDelay.Value);
            printAtOnce = (int)options.numPrintAtOnce.Value;
            prompt = options.chkPrompt.Checked;
            blankLine = options.chkBlankLine.Checked;
            echo = options.chkEcho.Checked;
            echoColor = options.picEchoColor.BackColor;
            errorColor = options.picErrorColor.BackColor;

            todoDir = options.txtTodoDir.Text;
            todoHideDone = options.checkTodoHideDone.Checked;
            todoAutoTransfer = options.checkTodoAutoTransfer.Checked;
            todoMiscColor = options.picTodoMiscColor.BackColor;
            todoItemColor = options.picTodoItemColor.BackColor;
            todoDoneColor = options.picTodoDoneColor.BackColor;

            twUpdateOnNewTweet = options.checkTwCountOnNewTweet.Checked;
            twUpdateOnFocus = options.checkTwCountOnFocus.Checked;
            minTweetPeriod = (int)options.numTwCountMinPeriod.Value;
            twUserColor = options.picTwUsernameColor.BackColor;
            twMiscColor = options.picTwMiscColor.BackColor;
            twTweetColor = options.picTwTweetColor.BackColor;
            twLinkColor = options.picTwLinkColor.BackColor;
            twTimeColor = options.picTwTimestampColor.BackColor;
            twCountColor = options.picTwCountColor.BackColor;

            mailUser = options.txtUser.Text;
            mailPass = options.txtPass.Text;
            mailUpdateOnNewMail = options.checkMailCountOnNewMail.Checked;
            mailUpdateOnFocus = options.checkMailCountOnFocus.Checked;
            timerMailCheck.Interval = (int)options.numMailCheckPeriod.Value * 60000;
            mailCountColor = options.picMailCountColor.BackColor;
            mailHeaderColor = options.picMailHeaderColor.BackColor;
            mailSummaryColor = options.picMailSummaryColor.BackColor;

            //apply changes & save
            Hotkey.RegisterHotKey(this, hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift);

            this.Top = y;
            this.Left = x;

            windowAutosize();
            setPrompt();

            gmail.ChangeLogin(mailUser, mailPass);

            saveOptions();
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
            if (prompt)
            {
                if (twitterOutput)
                    lblPrompt.Text = "Twitter>";
                else if (inputMode == "twitter pin")
                    lblPrompt.Text = "Twitter PIN:";
                else
                    lblPrompt.Text = dir + ">";

                lblPrompt.Visible = true;
                lblPrompt.Top = txtCMD.Top;
                txtCMD.Left = lblPrompt.Width;
            }
            else
            {
                lblPrompt.Visible = false;
                txtCMD.Left = 0;
            }
        }

        void setPrompt(string custom)
        {
            if (prompt)
            {
                lblPrompt.Text = custom + ">";

                lblPrompt.Visible = true;
                lblPrompt.Top = txtCMD.Top;
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
                case "user":
                    user = txtCMD.Text;
                    saveOptions();

                    //continue with first time intro
                    greeting();
                    print("This seems to be the first time you are running Adjutant. Would you like to run the tutorial/setup? (y/n)");

                    inputMode = "tutorial";
                    txtCMD.Text = "";
                    break;
                case "twitter pin":
                    if (txtCMD.Text == "")
                        return;

                    OAuthAccessToken access = twitter.GetAccessToken(requestToken, txtCMD.Text);
                    token = access.Token;
                    secret = access.TokenSecret;

                    if (token != "?" && secret != "?")
                    {
                        twitter.AuthenticateWith(token, secret);
                        saveOptions();

                        print("Adjutant has been successfully authorized.");

                        tweetCount();

                        if (tweets.Count != 0)
                            twitterPrint();
                    }
                    else
                        print("Authorization failed. Please try again.");

                    txtCMD.Text = "";
                    inputMode = "default";
                    setPrompt();
                    break;
                case "process redirect":
                    print(txtCMD.Text);

                    if (proc != null)
                        proc.StandardInput.WriteLine(txtCMD.Text);
                    else
                    {
                        string procName = lblPrompt.Text;
                        if (procName.Length > 0)
                            procName = procName.Substring(0, procName.Length - 1);

                        print("The process " + procName + " has terminated.", errorColor);

                        inputMode = "default";
                        setPrompt();
                    }

                    txtCMD.Text = "";
                    break;
                case "tutorial":
                    if (txtCMD.Text.ToLower() == "y" || txtCMD.Text.ToLower() == "yes")
                    {
                        //TODO TUTORIAL
                    }

                    txtCMD.Text = "";
                    inputMode = "default";
                    break;
                default:
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
                            case"calc":
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
                            case "twitter":
                            case "tw":
                                cmdTwitter(cmd);
                                break;
                            case "mail":
                                cmdMail(cmd);
                                break;
                            case "":
                                flush();
                                break;
                            default:
                                //replace custom commands
                                foreach (var customCmd in customCmds)
                                    txtCMD.Text = txtCMD.Text.Replace(customCmd.Key, customCmd.Value);

                                runProcess(txtCMD.Text);
                                break;
                        }
                    }
                    catch (Exception exc)
                    {
                        printError("Error while executing command.", exc);
                    }

                    if (txtCMD.Text != "")
                    {    
                        //add to command history
                        history.Add(txtCMD.Text);
                        historyInd = 0;

                        txtCMD.Text = "";
                    }
                    break;
            }
        }

        void cmdHelp(string[] cmd)
        {
            if (cmd.Length == 1)
            {
                print("List of Adjutant commands: " + Environment.NewLine);
                foreach (string command in new string[] { "about", "calc", "cd", "cls", "custom", "date", "done", "exit", "help", "mail", "prompt", "time", "todo", "twitter" })
                    print(command);
                print("For more information on a specific command, type \"help <COMMAND>\"");
            }
            else
            {
                switch (cmd[1])
                {
                    case "about":
                        print("Displays information about Adjutant.");
                        break;
                    case "calc":
                        print("Starts Windows Calculator");
                        break;
                    case "cd":
                        print("Changes current directory.");
                        break;
                    case "cls":
                        print("Clears screen (console).");
                        break;
                    case "custom":
                        print("Use this command to save your custom commands, file or folder paths, URLs, or any other string constants.");
                        print("Add a new custom command with the following syntax: \"custom [command_name]=[command_string]\".");
                        print("To view the current custom commands just call the command without any other arguments: \"custom\".");
                        print("To delete a custom command use this syntax: \"custom /del [command_name]\".");
                        break;
                    case "date":
                        print("Gets current date.");
                        break;
                    case "done":
                        print("Use \"done\" to mark tasks in your todo lists as completed.");
                        print("\"done [task_index]\" marks the task with that index (in today's todo list) as finished.");
                        print("\"done [task_index] /tomorrow OR /t\" marks the task with that index (in tomorrow's todo list) as finished.");
                        print("\"done [task_index] /date=YYYY-MM-DD OR /d=YYYY-MM-DD\" marks the task with that index (in a specific date's todo list) as finished.");
                        print("You can specify more than one index at a time by separating them with commas.");
                        print("To specify a sequential range of numbers separate the bounds with a hyphen.");
                        print("For example: \"done 4,8,15-16,23,42,100-108\"");
                        print("To add new tasks or view task lists use the \"todo\" command.");
                        break;
                    case "exit":
                        print("Shuts down Adjutant.");
                        break;
                    case "help":
                        print("H E L P C E P T I O N");
                        break;
                    case "mail":
                        print("The \"mail\" command allows you to check your Gmail account for new messages.");
                        print("To setup your account, use the following command: \"mail /setup [username] [password]\".");
                        //print("Calling the command will .");
                        print("To change how often Adjutant checks for new mail, as well as other options, go to the Options menu.");
                        break;
                    case "time":
                        print("Gets current time.");
                        break;
                    case "todo":
                        print("The \"todo\" command implements a simple todo list manager.");
                        print("Use it to add new tasks or view task lists.");
                        print("\"todo [task]\" adds a new task for today.");
                        print("\"todo [task] /tomorrow OR /t\" adds a new task to tomorrow's todo list.");
                        print("\"todo [task] /date=YYYY-MM-DD OR /d=YYYY-MM-DD\" adds a new task for a specific date.");
                        print("\"todo\" displays unfinished tasks for today.");
                        print("\"todo /tomorrow OR /t\" displays unfinished tasks for tomorrow.");
                        print("\"todo /date=YYYY-MM-DD OR /d=YYYY-MM-DD\" displays unfinished tasks for a specific day.");
                        print("\"todo /list OR /l\" displays all todo lists from this date onward.");
                        print("\"todo /archive OR /a\" displays past todo lists.");
                        print("To check tasks as completed use the \"done\" command.");
                        break;
                    case "twitter":
                    case "tw":
                        if (cmd.Length < 3 || !cmd[2].ToLower().Contains("/i"))
                        {
                            print("This command allows you to read the tweets in your home timeline.");
                            print("You can also use the shorter keyword \"tw\".");
                            print("");
                            print("Calling the command will display a tweet (if there are any) and will lock input into Adjutant.");
                            print("Press the 'o' button to open all URLs in the tweet (You can also click them with the mouse).");
                            print("Press the 'j' button to read the next tweet.");
                            print("Press the 'a' button to print all tweets at once.");
                            print("Press the 'Escape' button to exit Twitter mode and enable standard input again.");
                            print("");
                            print("You can also print out all tweets with the following command: \"twitter /all\"");
                            print("");
                            print("Before using the Twitter service you will have to authorize Adjutant to view your tweets.");
                            print("The authorization should begin automatically; if not, enter the following command: \"twitter /init\"");
                            print("If you run into any problems with authorization, you can get more information by typing the following: \"help twitter /init\"");
                            print("To change other settings, go to the Options menu.");
                        }
                        else
                        {
                            print("To authorize Adjutant to view your tweets, use the following command: \"twitter /init\"");
                            print("If the \"twitter\" command stopped working, there could be several different causes for the problem:");
                            print("Your Internet connection could be down.");
                            print("www.twitter.com could be temporarily unavailable.");
                            print("Adjutant's authorization might no longer be valid. Try authorizing it again.");
                        }
                        break;
                    case "options":
                        print("Opens the options window.");
                        break;
                    case "prompt":
                        print("Toggles prompt.");
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

            //init buffer & gfx
            context.MaximumBuffer = new Size(this.Width + 1, this.Height + 1);
            grafx = context.Allocate(this.CreateGraphics(), new Rectangle(0, 0, this.Width, this.Height));

            //calc window height
            MeasureLineHeight();
            maxLines = txtCMD.Top / lineH;
        }

        void jumpToLastLine()
        {
            if (lastChunk == -1)
                chunkOffset = 0;
            else
            {
                do
                {
                    chunkOffset = lastChunk;
                    int countedLines = 0;

                    while (chunkOffset > 0 && countedLines < maxLines)
                    {
                        chunkOffset--;

                        if (chunks[chunkOffset].IsNewline())
                            countedLines++;

                        //move to the first chunk in this line
                        while (chunkOffset > 0 && !chunks[chunkOffset - 1].IsNewline())
                            chunkOffset--;
                    }

                    if (countedLines == maxLines && this.Height + lineH <= maxH)
                    {
                        //increase console height by 1 line
                        this.Height += lineH;
                        windowAutosize();
                    }
                    else 
                    {
                        if (this.Height + lineH > maxH && countedLines == maxLines)
                            //skip 1 line
                            do
                            {
                                if (chunkOffset == chunks.Count - 1)
                                    break;
                                else
                                    chunkOffset++;
                            }
                            while (!chunks[chunkOffset - 1].IsNewline());

                        break; //console height is fine or can't increase it anymore, so exit the loop
                    }
                } while (true);
            }

            //ensure adjutant remains hidden
            if (hidden && hideStyle == HideStyle.ScrollUp)
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

        void cls()
        {
            timerPrint.Enabled = false;

            chunks.Clear();
            lastChunk = 0;
            lastChunkChar = 0;

            this.Height = minH;
            
            windowAutosize();
            update();
        }

        void print(string txt)
        {
            print(txt, "", true, txtCMD.ForeColor);
        }

        void print(string txt, bool newline)
        {
            print(txt, "", newline, txtCMD.ForeColor);
        }

        void print(string txt, Color color)
        {
            print(txt, "", true, color);
        }

        void print(string txt, bool newline, Color color)
        {
            print(txt, "", newline, color);
        }

        void print(string txt, string link, Color color)
        {
            print(txt, link, true, color);
        }

        void print(string txt, string link, bool newline, Color color)
        {
            if (txt == "")
                txt = " "; //blank line

            if (lastChunk == -1)
                lastChunk = 0;

            if (timerPrint.Interval == ZERO_DELAY)
                txt = txt.Replace("<pause>", "");
            else
                txt = txt.Replace("<pause>", Environment.NewLine + "<pause>" + Environment.NewLine);

            string[] lines = txt.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                bool nLine = i == lines.Length - 1 ? newline : true;
                List<TextChunk> newChunks = TextChunk.Chunkify(lines[i], link, color, leftMargin, this.Width, txtCMD.Font, nLine, nLine, MeasureWidth, lineH);

                chunks.AddRange(newChunks);

                if (nLine)
                    leftMargin = 0;
                else
                {
                    if (newChunks.Count > 1)
                        leftMargin = 0;
                    leftMargin += MeasureWidth(newChunks[newChunks.Count - 1].ToString());
                }
            }

            if (timerPrint.Interval != ZERO_DELAY)
            {
                if (this.InvokeRequired)
                    MessageBox.Show("invoke required");

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
            {
                lblPrompt.Text = dir + ">";
                txtCMD.Left = lblPrompt.Width;
            }
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

            //determine type of process
            ProcessStartInfo procInfo;

            if (File.Exists(procName) && Path.GetExtension(procName) != ".vbs")
            {
                if (Path.GetExtension(procName) == ".py")
                {
                    //it's a python script
                    if (python != "")
                    {
                        //procInfo = new ProcessStartInfo(@"cmd.exe", @"/C " + python + " \"" + procName + "\" " + args + cmd);
                        procInfo = new ProcessStartInfo(python, " \"" + procName + "\" " + args);
                        inputMode = "process redirect";
                    }
                    else
                    {
                        print("Could not start script.<pause>Python was not found on your system.", errorColor);
                        return;
                    }
                }
                else
                {
                    //it's a file (but not a vbs/py script)
                    procInfo = new ProcessStartInfo(procName, args);
                    inputMode = "process redirect";
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
                    procInfo = new ProcessStartInfo(@"cmd.exe", @"/C" + cmd);
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

            if (inputMode == "process redirect")
            {
                //set events for process output and redirect user input to the process
                setPrompt(Path.GetFileNameWithoutExtension(procInfo.FileName));

                proc = Process.Start(procInfo);

                proc.OutputDataReceived += new DataReceivedEventHandler(proc_DataReceived);
                proc.ErrorDataReceived += new DataReceivedEventHandler(proc_ErrorDataReceived);
                proc.Exited += new EventHandler(proc_Exited);

                proc.EnableRaisingEvents = true;
                proc.SynchronizingObject = this;

                proc.BeginOutputReadLine();
            }
            else
            {
                //just print the output
                proc = Process.Start(procInfo);
                
                StreamReader outputStream = proc.StandardOutput;
                string output = outputStream.ReadToEnd();
                proc.WaitForExit(1000);

                if (output != "")
                    print(output);
                else
                    print("Unrecognized command.", errorColor);
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
            inputMode = "default";
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
                        this.Height = my;
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
                    this.Cursor = Cursors.Default;
            }
        }

        void mouseUp(int mx, int my)
        {
            if (resizeH)
            {
                if (!ctrlKey)
                    minH = this.Height;
                else
                    maxH = this.Height;

                windowAutosize();
            }
            else if (drag)
            {
                x = this.Left;
                y = this.Top;
            }

            if (drag || resizeW || resizeH)
            {
                saveOptions();

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

            //return contents.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList().FindAll(t => t.Length < 8 || t.Substring(0, 8) != "__DONE__");
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

                if (todaysTodo >= 1)
                {
                    todo.AddRange(tasklist(todos[todaysTodo - 1]).FindAll(item => !item.Contains("__DONE__") && !todo.Contains(item) && !todo.Contains("__DONE__" + item)));

                    StreamWriter file = new StreamWriter(todoFile("today"));
                    foreach (string item in todo)
                        file.WriteLine(item);
                    file.Close();
                }
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
                            print(ind++ + ". " + task.Replace("__DONE__", ""), todoDoneColor);
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
                else
                    if (dt.Date == DateTime.Today.AddDays(1))
                        date = "tomorrow";
            }

            print("To do list for " + date + ":", todoFile(date), todoMiscColor);

            int ind = 1;
            foreach (string task in tasklist(todoFile(date)))
                if (task.Contains("__DONE__"))
                    print(ind++ + ". " + task.Replace("__DONE__", ""), todoDoneColor);
                else
                    print(ind++ + ". " + task, todoItemColor);
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
            string task = txtCMD.Text.Substring(txtCMD.Text.IndexOf(' ') + 1);

            //load target todo list
            string date = "today";

            if (task.Contains('/'))
            {
                if (task.Contains("/t"))
                    date = "tomorrow";
                else if (task.Contains("/d"))
                {
                    int lb = task.IndexOf('=', task.IndexOf("/d"));

                    if (lb != -1)
                    {
                        date = task.Substring(lb + 1);
                        task = task.Substring(0, task.IndexOf('/'));
                    }
                }

                task = task.Substring(0, task.IndexOf('/'));
            }

            List<string> tasks = tasklist(todoFile(date));

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
                        //skip done items
                        while (actualIndex < tasks.Count && tasks[actualIndex].Contains("__DONE__"))
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
                    print(tasks[dn] + " DONE", todoDoneColor);
                    tasks[dn] = "__DONE__" + tasks[dn];
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
        }
        #endregion

        #region Twitter Module
        void twitterAuth()
        {
            print("You need to authorize Adjutant (console) before you can use it as a Twitter client.");
            print("The authorization URL has been launched in your browser.");
            print("Please click on \"Authorize app\" and then copy the given PIN into Adjutant.");

            if (twitter == null)
                twitter = new TwitterService("bQp3ytw07Ld9bmEBU4RI4w", "IXS35cJodk8aUVQO26vTYUb61kYbIV6cznOYBd7k7AI");

            requestToken = twitter.GetRequestToken();
            Uri uri = twitter.GetAuthorizationUri(requestToken);
            Process.Start(uri.ToString());

            inputMode = "twitter pin";
            setPrompt();
        }

        void twitterInit()
        {
            twitter = new TwitterService("bQp3ytw07Ld9bmEBU4RI4w", "IXS35cJodk8aUVQO26vTYUb61kYbIV6cznOYBd7k7AI");

            if (token != "?" && secret != "?")
            {
                twitter.AuthenticateWith(token, secret);

                if (twitter.VerifyCredentials(new VerifyCredentialsOptions()) == null)
                {
                    //authentication failed
                    twitter = null;
                    print("Error while initializing Twitter service. To see more information about this error please type the following command: \"help twitter /init\"", errorColor);
                }
                else
                {
                    twitter.StreamUser(NewTweet);

                    if (lastTweet != -1)
                    {
                        var options = new ListTweetsOnHomeTimelineOptions();

                        if (lastTweet != 0)
                            options.SinceId = lastTweet;
                        options.Count = 1000;

                        try
                        {
                            IEnumerable<TwitterStatus> tweetList = twitter.ListTweetsOnHomeTimeline(options);
                            if (tweetList != null)
                                foreach (var tweet in tweetList.Reverse<TwitterStatus>())
                                    tweets.Add(new Tweet(tweet.User.Name, tweet.User.ScreenName, Regex.Unescape(WebUtility.HtmlDecode(tweet.Text)), tweet.CreatedDate, tweet.Id));
                        }
                        catch
                        {
                            print("Unidentified error with Twitter module.", errorColor);
                        }
                    }
                }
            }
        }

        string getField(string response, string key)
        {
            int lb = response.IndexOf("\"" + key + "\":\"") + key.Length + 4;
            int ub = response.IndexOf("\",\"", lb);

            return response.Substring(lb, ub - lb);
        }

        string howLongAgo(DateTime time)
        {
            TimeSpan diff = DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(time);

            if (diff.TotalSeconds < 1)
                return diff.Milliseconds + " ms ago";
            else if (diff.TotalMinutes < 1)
                return diff.Seconds + " secs ago";
            else if (diff.TotalHours < 1)
                return diff.Minutes + " mins ago";
            else if (diff.TotalDays < 1)
                return diff.Hours + " hrs ago";
            else if (diff.TotalDays < 2)
                return "Yesterday";
            else
                return diff.TotalDays + " days ago";
        }

        string getNextURL(string tweet, out int ind)
        {
            string match = Regex.Match(tweet, @"((http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)").ToString();

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
            if (tweets.Count > 0)
            {
                //print "<username> tweeted "
                print(tweets[0].user, "https://twitter.com/" + tweets[0].actualUsername, false, twUserColor);
                print(" tweeted ", false, twMiscColor);

                //split tweet into chunks in order to color mentions and links
                string tweet = tweets[0].text;
                twURLs.Clear();

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

                        tweet = tweet.Remove(0, mentionInd + mention.Length);
                    }

                    url = getNextURL(tweet, out urlInd);
                    mention = getNextMentionOrHashtag(tweet, out mentionInd);
                }

                if (tweet != "")
                    print(tweet, false, twTweetColor);

                //print timestamp
                print(" " + howLongAgo(tweets[0].created), "https://twitter.com/" + tweets[0].actualUsername + "/status/" + tweets[0].id, twTimeColor);

                lastTweet = tweets[0].id;

                //remove tweet
                tweets.RemoveAt(0);

                twitterOutput = tweets.Count > 0;
                if (!twitterOutput)
                    print("No more tweets.");

                setPrompt();
            }
        }

        void twitterPrint(bool all)
        {
            if (!all)
                twitterPrint();
            else
                while (tweets.Count > 0)
                    twitterPrint();
        }

        void tweetCount()
        {
            string tweetCount = tweets.Count + " new tweet" + (tweets.Count == 1 ? "." : "s.");

            if (chunks.Count == 0 || chunks[chunks.Count - 1].ToString() != tweetCount)
                print(tweetCount, "http://www.twitter.com/", twCountColor);

            lastTwCount = DateTime.Now;
        }

        private void NewTweet(TwitterStreamArtifact streamEvent, TwitterResponse response)
        {
            if (!response.Response.Contains("text"))
                return;

            long id = long.Parse(getField(response.Response, "id_str"));

            string created = getField(response.Response, "created_at");
            created = created.Substring(created.IndexOf(' '));
            created = created.Substring(created.Length - 4, 4) + created.Substring(0, created.IndexOf(" +"));

            DateTime time = DateTime.Parse(created);
            time = TimeZone.CurrentTimeZone.ToLocalTime(time);

            string user = getField(response.Response, "name");
            string actualUsername = getField(response.Response, "screen_name");
            string tweet = " tweeted " + Regex.Unescape(WebUtility.HtmlDecode(getField(response.Response, "text")));

            if (twUpdateOnNewTweet && DateTime.Now.Subtract(lastTwCount).TotalSeconds >= minTweetPeriod)
            {
                if (activated)
                {
                    if (tweets.Count == 0)
                    {
                        print(tweet + " " + howLongAgo(time), true);
                        lastTweet = id;
                    }
                    else
                    {
                        tweets.Add(new Tweet(user, actualUsername, tweet, time, id));
                        tweetCount();
                    }
                }
                else
                    tweets.Add(new Tweet(user, actualUsername, tweet, time, id));
            }
            else
                tweets.Add(new Tweet(user, actualUsername, tweet, time, id));
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
                    print("To see more information about this error please type the following command: \"help twitter /init\"", errorColor);
                }
            }
            else
            {
                if (cmd.Length == 1)
                {
                    tweetCount();

                    if (tweets.Count != 0)
                        twitterPrint();
                }
                else if (cmd[1].ToLower().Contains("/a"))
                    twitterPrint(true);
                else if (cmd[1].ToLower().Contains("/i"))
                    twitterAuth();
            }
        }
        #endregion

        #region Mail Module
        void mailInit()
        {
            if (!string.IsNullOrEmpty(mailUser) && !string.IsNullOrEmpty(mailPass))
            {
                gmail = new Gmail(mailUser, mailPass);
                getNewMailCount();

                if (newMailCount != -1)
                {
                    displayNewMailCount();
                    timerMailCheck.Enabled = true;
                }
            }
        }

        void getNewMailCount()
        {
            newMailCount = gmail.Check();
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
        }

        void displayNewMail(bool verbose)
        {
            print("Gmail - Inbox for " + mailUser);

            foreach (string[] email in gmail.emails)
                if (!verbose)
                    print(email[gmail.M_SENDER] + ": " + email[gmail.M_TITLE] + " - " + email[gmail.M_DATE], email[gmail.M_LINK], mailHeaderColor);
                else
                {
                    print(email[gmail.M_SENDER] + ": " + email[gmail.M_TITLE] + " - " + email[gmail.M_DATE], email[gmail.M_LINK], mailHeaderColor);
                    print(email[gmail.M_SUMMARY], mailSummaryColor);
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
            else
            {
                bool verbose = cmd.Length > 1 && cmd[1].Contains("/v");
                displayNewMail(verbose);
            }
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
                activateConsole();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            grafx.Render(e.Graphics);
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
                //timerShowHide.Enabled = false;

                this.Opacity = opacityActive;
                autohideTrigger();
            }
            else if (!txtCMD.Focused)
                this.Opacity = opacityPassive;
        }

        private void formMain_Load(object sender, EventArgs e)
        {
            //tray context menu
            trayIcon.ContextMenuStrip = contextMenu;

            //buffer context
            context = BufferedGraphicsManager.Current;

            //load options; init window & gfx
            loadOptions();
            windowAutosize();

            loadCustomCmds();

            link = "";

            //get python exe path
            RegistryKey pyKey = Registry.ClassesRoot.OpenSubKey(@"Python.File\shell\open\command");

            if (pyKey != null)
                python = pyKey.GetValue("", "").ToString();
            else
                python = "";

            if (python != "")
            {
                if (python[0] == '"' && python.IndexOf('"', 1) != -1)
                    python = python.Substring(1, python.IndexOf('"', 1) - 1);
                else
                {
                    python = python.Replace("%1", "").Replace("%*", "");
                    python = python.Replace("\"\"", "");
                    python = python.Replace("  ", " ");
                }
            }

            //first run
            if (user == "first_run")
            {
                print("Welcome to Adjutant!<pause>");
                print("What is your name?");
                inputMode = "user";
                return;
            }

            //print intro
            print("Adjutant online.<pause>");
            greeting();
            todoLoad();

            //twitter init
            twitterInit();

            //mail init
            mailInit();
        }

        private void formMain_Activated(object sender, EventArgs e)
        {
            if (!initialized)
            {
                //global hotkey & mouse tracker
                //this is initialized here instead of in Form_Load becase there it causes major mouse lag and general slowiness
                actHook = new UserActivityHook();
                actHook.KeyDown += new KeyEventHandler(ActivityHook_KeyDown);
                actHook.KeyUp += new KeyEventHandler(ActivityHook_KeyUp);
                actHook.OnMouseActivity += new MouseEventHandler(ActivityHook_OnMouseActivity);

                initialized = true;
            }

            activated = true;

            txtCMD.Focus();

            if (tweets.Count > 0 && twUpdateOnFocus && DateTime.Now.Subtract(lastTwCount).TotalSeconds >= minTweetPeriod)
                tweetCount();

            if (mailUpdateOnFocus && newMailCount > 0)
                displayNewMailCount();
        }

        private void formMain_Deactivate(object sender, EventArgs e)
        {
            activated = false;
            this.Opacity = opacityPassive;
        }

        private void formMain_Resize(object sender, EventArgs e)
        {
            int lMarg = 0;

            //recalculate text chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                //can join with next chunk(s)?
                while (i < chunks.Count - 1 && chunks[i].JoinChunk(chunks[i + 1], this.Width, txtCMD.Font, MeasureWidth))
                    chunks.RemoveAt(i + 1);

                //need to split chunk?
                List<TextChunk> newChunks = TextChunk.Chunkify(chunks[i].ToString(), chunks[i].GetLink(), chunks[i].GetColor(), lMarg, this.Width, txtCMD.Font, true, chunks[i].IsAbsNewline(), MeasureWidth, lineH);

                if (newChunks.Count > 1)
                {
                    chunks.RemoveAt(i);

                    foreach (TextChunk chunk in newChunks)
                        chunks.Insert(i++, chunk);

                    i--;

                    lMarg = 0;
                }

                if (chunks[i].IsNewline())
                    lMarg = 0;
                else
                    lMarg += MeasureWidth(chunks[i].ToString());
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
                //resize buffers, graphics, etc.
                context.MaximumBuffer = new Size(this.Width + 1, this.Height + 1);

                if (grafx != null)
                {
                    grafx.Dispose();
                    grafx = null;
                }

                grafx = context.Allocate(this.CreateGraphics(), new Rectangle(0, 0, this.Width, this.Height));

                draw(grafx.Graphics);
                this.Refresh();
            }
        }

        private void formMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            saveOptions();
            Hotkey.UnregisterHotKey(this);
        }

        private void formMain_KeyDown(object sender, KeyEventArgs e)
        {
            ctrlKey = e.Control;
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

            //let the other chunks know the mouse isn't over them
            for (i++; i < chunks.Count; i++)
                chunks[i].MouseNotOver();

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
            //scroll up/down 3 lines
            int i = 0;

            if (e.Delta < 0)
                while (i < 3)
                {
                    if (chunkOffset == chunks.Count - 1)
                        break;

                    if (chunks[chunkOffset].IsNewline())
                        i++;

                    chunkOffset++;
                }
            else
                while (i < 3)
                {
                    if (chunkOffset == 0)
                        break;

                    chunkOffset--;

                    if (chunks[chunkOffset].IsNewline())
                        i++;

                    //move to the first chunk in this line
                    while (chunkOffset > 0 && !chunks[chunkOffset - 1].IsNewline())
                        chunkOffset--;
                }

            draw(grafx.Graphics);
            this.Refresh();
        }

        private void txtCMD_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                mouseDown(txtCMD.Left + e.X, txtCMD.Top + e.Y);
        }

        private void txtCMD_MouseMove(object sender, MouseEventArgs e)
        {
            mouseMove(txtCMD.Left + e.X, txtCMD.Top + e.Y);
        }

        private void txtCMD_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                mouseUp(txtCMD.Left + e.X, txtCMD.Top + e.Y);
        }

        private void txtCMD_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            //prepare autoinactivate/autohide
            timerShowHide.Enabled = false;
            this.Opacity = opacityActive;

            autohideTrigger();

            if (twitterOutput)
                switch (e.KeyCode)
                {
                    case Keys.Enter:
                        flush();
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
                    case Keys.Escape:
                        twitterOutput = false;
                        setPrompt();
                        tweetCount();
                        break;
                }
            else
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
                        update();
                        pauseEnd = DateTime.Now.AddMilliseconds(750);
                        chunks.RemoveAt(lastChunk);
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
                            timerShowHide.Enabled = false;
                            hidden = false;
                        }
                    }
                    break;
            }
        }

        private void timerMailCheck_Tick(object sender, EventArgs e)
        {
            getNewMailCount();

            if (mailUpdateOnNewMail)
                displayNewMailCount();
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