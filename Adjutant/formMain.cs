using System;
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
        const int SND_ASYNC = 0x0001;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("WinMM.dll")]
        static extern bool PlaySound(string fname, int Mod, int flag);

        enum HideStyle { Disappear, Fade, ScrollUp, ScrollDown, ScrollLeft, ScrollRight };

        HideStyle hideStyle;
        Gmail gmail;
        Twitter twitter;
        UserActivityHook actHook;
        formOptions options;
        Process proc;
        BufferedGraphicsContext context;
        BufferedGraphics grafx;
        Brush brush = Brushes.White;
        Color echoColor, errorColor, helpColor, todoMiscColor, todoItemColor, todoDoneColor, twUserColor, twMiscColor, twTweetColor, twLinkColor, twTimeColor, twCountColor, mailCountColor, mailHeaderColor, mailSummaryColor;
        DateTime autoHide, pauseEnd, lastTwCount;
        List<Chunk> chunks = new List<Chunk>();
        Dictionary<string, string> customCmds;
        List<string> history = new List<string>(), twURLs = new List<string>(), twMentions = new List<string>(), todo;
        List<int> printingImgs = new List<int>();
        string[] filteredPaths;
        string python, user, dir, todoDir, inputMode, token, secret, link, mailUser, mailPass, twUsername, twSound, mailSound;
        double opacityPassive, opacityActive;
        long lastTweet;
        int x, y, lineH, currImgChunkH, minH, maxH, prevH, prevX, prevY, leftMargin, yOffset, chunkOffset, lastChunk, lastChunkChar, printAtOnce, autoHideDelay, tabInd, historyInd, minTweetPeriod, twSoundThreshold, hotkey, newMailCount, prevNewMailCount, mailSoundThreshold, tutorialStep;
        bool initialized, winKey, prompt, blankLine, echo, ctrlKey, drag, resizeW, resizeH, autoResize, hiding, hidden, todoHideDone, todoAutoTransfer, twUpdateOnNewTweet, twUpdateOnFocus, twOutput, twPrevCountBelowThreshold, mailUpdateOnNewMail, mailUpdateOnFocus, hotkeyCtrl, hotkeyAlt, hotkeyShift;
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

        void toggleSelectMode()
        {
            if (!txtSelection.Visible)
            {
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

                    txtSelection.Font = txtCMD.Font;
                    txtSelection.BackColor = txtCMD.BackColor;
                    txtSelection.ForeColor = txtCMD.ForeColor;
                    txtSelection.Width = this.Width;
                    txtSelection.Height = txtCMD.Top;
                    txtSelection.Visible = true;
                    
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
            }
            else
            {
                //hide selection txtbox
                txtSelection.Visible = false;
                
                txtCMD.Focus();
            }
        }

        void draw(Graphics gfx)
        {
            gfx.Clear(this.BackColor);

            if (chunks.Count > 0)
            {
                int x = 0, y = yOffset, h = 0;
                int chInd = chunkOffset;

                while (chInd < lastChunk && y < txtCMD.Top)
                    chunks[chInd++].Draw(gfx, txtCMD.Font, ref x, ref y, ref h);
                chunks[lastChunk].Draw(gfx, txtCMD.Font, ref x, ref y, ref h, lastChunkChar);
            }
        }

        int measureWidth(string txt)
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
            file.WriteLine("tw_sound=" + twSound);
            file.WriteLine("tw_sound_threshold=" + twSoundThreshold);
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
            file.WriteLine("mail_sound=" + mailSound);
            file.WriteLine("mail_sound_threshold=" + mailSoundThreshold);
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
                options.chkTodoHideDone.Tag = todoHideDone;
                options.chkTodoAutoTransfer.Tag = todoAutoTransfer;
                options.picTodoMiscColor.Tag = todoMiscColor;
                options.picTodoItemColor.Tag = todoItemColor;
                options.picTodoDoneColor.Tag = todoDoneColor;

                options.chkTwCountOnNewTweet.Tag = twUpdateOnNewTweet;
                options.chkTwCountOnFocus.Tag = twUpdateOnFocus;
                options.numTwCountMinPeriod.Tag = minTweetPeriod;
                options.txtTwSound.Tag = twSound;
                options.numTwSoundThreshold.Tag = twSoundThreshold;
                options.picTwUsernameColor.Tag = twUserColor;
                options.picTwMiscColor.Tag = twMiscColor;
                options.picTwTweetColor.Tag = twTweetColor;
                options.picTwLinkColor.Tag = twLinkColor;
                options.picTwTimestampColor.Tag = twTimeColor;
                options.picTwCountColor.Tag = twCountColor;

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

                options.chkTwCountOnNewTweet.Checked = twUpdateOnNewTweet;
                options.chkTwCountOnFocus.Checked = twUpdateOnFocus;
                options.numTwCountMinPeriod.Value = minTweetPeriod;
                options.txtTwSound.Text = twSound;
                options.numTwSoundThreshold.Value = twSoundThreshold;
                options.picTwUsernameColor.BackColor = twUserColor;
                options.picTwMiscColor.BackColor = twMiscColor;
                options.picTwTweetColor.BackColor = twTweetColor;
                options.picTwLinkColor.BackColor = twLinkColor;
                options.picTwTimestampColor.BackColor = twTimeColor;
                options.picTwCountColor.BackColor = twCountColor;

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

            Font prevFont = lblPrompt.Font;

            lblPrompt.Font = new Font(options.comboFont.Text, (float)options.numFontSize.Value, style);
            txtCMD.Font = new Font(options.comboFont.Text, (float)options.numFontSize.Value, style);
            lblPrompt.ForeColor = options.picTextColor.BackColor;
            txtCMD.ForeColor = options.picTextColor.BackColor;

            if (lblPrompt.Font != prevFont)
            {
                this.OnResize(new EventArgs());

                foreach (Chunk chunk in chunks)
                    chunk.SetTextBounds(new Rectangle(0, 0, measureWidth(chunk.ToString()), lineH));

                update();
            }

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

            twUpdateOnNewTweet = options.chkTwCountOnNewTweet.Checked;
            twUpdateOnFocus = options.chkTwCountOnFocus.Checked;
            minTweetPeriod = (int)options.numTwCountMinPeriod.Value;
            twSound = options.txtTwSound.Text;
            twSoundThreshold = (int)options.numTwSoundThreshold.Value;
            twUserColor = options.picTwUsernameColor.BackColor;
            twMiscColor = options.picTwMiscColor.BackColor;
            twTweetColor = options.picTwTweetColor.BackColor;
            twLinkColor = options.picTwLinkColor.BackColor;
            twTimeColor = options.picTwTimestampColor.BackColor;
            twCountColor = options.picTwCountColor.BackColor;

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

            //apply changes & save
            Hotkey.RegisterHotKey(this, hotkey, hotkeyCtrl, hotkeyAlt, hotkeyShift);

            this.Top = y;
            this.Left = x;

            windowAutosize();
            setPrompt();

            if (gmail != null)
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
                if (twOutput)
                    lblPrompt.Text = "Twitter>";
                else if (inputMode == "twitter pin")
                    lblPrompt.Text = "Twitter PIN:";
                else if (inputMode == "user")
                    lblPrompt.Text = "Username: ";
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
                case "user": //first time running -> awaiting username
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
                            inputMode = "tutorial";
                        }
                        else
                            inputMode = "default";

                        setPrompt();
                    }
                    break;
                case "twitter pin": //authorizing Twitter
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

                    inputMode = "default";
                    setPrompt();
                    break;
                case "process redirect": //forwarding input to external process
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
                    break;
                case "tutorial": //starting tutorial mode
                    if (txtCMD.Text.ToLower() == "exit")
                    {
                        inputMode = "default";
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
                                    inputMode = "default";
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

                                    inputMode = "default";
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
                                inputMode = "tutorial";
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
                                inputMode = "user";
                                setPrompt();
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
                foreach (string command in new string[] { "about", "calc", "cd", "cls", "custom", "date", "done", "exit", "help", "mail", "prompt", "time", "todo", "tutorial", "twitter", "user" })
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
                            printHelp("Note that you can also click on usernames, URLs, hashtags, etc, to open them in your browser.");
                            printHelp("");
                            printHelp("Press \"j\" to read the next tweet.");
                            printHelp("Press \"k\" to go back to the previous tweet.");
                            printHelp("Press \"a\" to print all tweets at once.");
                            printHelp("Press \"Escape\" to exit Twitter mode and enable standard input again.");
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

            //init buffer & gfx
            context.MaximumBuffer = new Size(this.Width + 1, this.Height + 1);
            grafx = context.Allocate(this.CreateGraphics(), new Rectangle(0, 0, this.Width, this.Height));

            //calc window height
            lineH = (int)grafx.Graphics.MeasureString("A", txtCMD.Font).Height;
        }

        void jumpToLastLine()
        {
            yOffset = 0;

            if (chunks.Count == 0 || lastChunk <= 0)
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
                    h = getChunkHeight(chunkInd);

                    while (chunkInd > 0 && !chunks[chunkInd - 1].IsNewline())
                    {
                        chunkInd--;

                        if (getChunkHeight(chunkInd) > h)
                            h = getChunkHeight(chunkInd);
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
            if (hidden && hideStyle == HideStyle.ScrollUp)
                this.Top = -this.Height + 1;
        }

        int getChunkHeight(int chunkInd)
        {
            if (printingImgs.Count > 0 && chunkInd == printingImgs[0])
                return currImgChunkH;
            else
                return chunks[chunkInd].GetHeight();
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
                    Chunk imgChunk = new Chunk(txt.Substring(lb, ub - lb), link, newline && ub == txt.Length - 1, newline);

                    //can img fit in current line?
                    if (leftMargin + imgChunk.GetWidth() > this.Width && chunks.Count > 0)
                        chunks[chunks.Count - 1].InsertNewline(); //nope, send img to next line

                    chunks.Add(imgChunk);

                    if (newline)
                        leftMargin = 0;
                    else
                        leftMargin += imgChunk.GetWidth();

                    if (imgChunk.GetHeight() > lineH)
                    {
                        //don't print the following chunks until this image has been displayed entirely (1 line at a time)
                        printingImgs.Add(chunks.Count - 1);
                        currImgChunkH = lineH;
                    }

                    imgChunk.AnimateGIF(new EventHandler(this.OnFrameChanged)); //if gif prepare animation

                    showNewChunks();

                    txt = txt.Substring(ub + 1);
                    if (txt == "")
                        return;
                }
                else
                    break;
            }

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
                List<Chunk> newChunks = Chunk.Chunkify(lines[i], link, color, strikeout, leftMargin, this.Width, txtCMD.Font, nLine, nLine, measureWidth, lineH);

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
                    if (python != "")
                    {
                        procInfo = new ProcessStartInfo(@"cmd.exe", "/C \"" + procName + "\" " + args);
                        //procInfo = new ProcessStartInfo(python, " \"" + procName + "\" " + args);
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
                else
                    if (dt.Date == DateTime.Today.AddDays(1))
                        date = "tomorrow";
            }

            print("To do list for " + date + ":", todoFile(date), todoMiscColor);

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

                inputMode = "twitter pin";
                setPrompt();
            }
        }

        void twitterInit()
        {
            if (token != "?" && secret != "?")
            {
                twitter = new Twitter(token, secret, lastTweet);
                twPrevCountBelowThreshold = true;

                if (!twitter.VerifyCredentials())
                {
                    print("Error while initializing Twitter service. To see more information about this error please type the following command: \"help twitter /init\"", errorColor);
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

                twOutput = twitter.AnyUnreadTweets();

                if (!twOutput)
                {
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
            } while (twOutput);
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
                && !twOutput) //or if Adjutant is currently in Twitter mode
            {
                print(tweetCountMsg, "http://www.twitter.com/", twCountColor);

                //play sound notification
                if (twSoundThreshold != 0 && tweetCount >= twSoundThreshold && twPrevCountBelowThreshold && File.Exists(twSound))
                {
                    PlaySound(twSound, 0, SND_ASYNC);
                    twPrevCountBelowThreshold = false;
                }
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
                    print("To see more information about this error please type the following command: \"help twitter /init\"", errorColor);
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

            txtSelection.MouseWheel += new System.Windows.Forms.MouseEventHandler(txtSelection_MouseWheel);

            link = "";
            helpColor = Color.Yellow;

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
                //create $path
                if (!customCmds.ContainsKey("$path"))
                    customCmds.Add("$path", Application.StartupPath);

                //display intro
                print("Welcome to Adjutant!<pause>");
                print("What is your name?");
                inputMode = "user";
                setPrompt();

                return;
            }

            //print intro
            print("Adjutant online.<pause>");
            greeting();
            todoLoad();

            //string kappa = @"<image=C:\Users\Winterstark\Desktop\Kappa.png>";

            //print(@"Grey Face - no space" + kappa + kappa + kappa);
            //print("IT WORKS!");
            //print(@"asdf <image=C:\Users\Winterstark\Desktop\~gypcg - Blue forest.jpg> 1234");

            //print(@"<image=C:\Users\Winterstark\Desktop\heroes.jpg>");
            //print("asdfasdfasdda");
            //print(@"<image=C:\Users\Winterstark\Desktop\heroes.jpg>");

            //print(@"Grey Face - no space" + kappa + kappa + kappa);
            //print("IT WORKS!");
            //print(@"asdf <image=C:\Users\Winterstark\Desktop\~gypcg - Blue forest.jpg> 1234");

            //print(@"Grey Face - no space" + kappa + kappa + kappa);
            //print("IT WORKS!");
            //print(@"asdf <image=C:\Users\Winterstark\Desktop\~gypcg - Blue forest.jpg> 1234");

            //print(@"<image=C:\Users\Winterstark\Desktop\heroes.jpg>");
            //print(@"<image=C:\Users\Winterstark\Desktop\heroes.jpg>");


            //print(@"<image=C:\dev\projex\Adjutant\Adjutant\bin\Debug\ui\loading.gif>");
            //print(@"<image=C:\Users\Winterstark\Desktop\saeOX53.gif>");

            //print(@"<image=C:\dev\projex\Adjutant\Adjutant\bin\Debug\ui\loading.gif>", false); print(" STILL THE SAME LINE", false);
            //print(@"<image=C:\dev\projex\Adjutant\Adjutant\bin\Debug\ui\loading.gif>", false); print(" STILL THE SAME LINE", false);
            //print(@"<image=C:\dev\projex\Adjutant\Adjutant\bin\Debug\ui\loading.gif>", false); print(" STILL THE SAME LINE?????", false);

            //print("<image=http://img.moviepilot.com/assets/tarantulaV2/long_form_background_images/1378462980_korra1.jpg>");

            ////twitter init
            //twitterInit();

            //////mail init
            //mailInit();
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

            txtCMD.Focus();

            if (twitter.AnyNewTweets() && twUpdateOnFocus && DateTime.Now.Subtract(lastTwCount).TotalSeconds >= minTweetPeriod)
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

            //note new width
            txtCMD.Width = this.Width;
            Chunk.ConsoleWidth = this.Width;

            int lMarg = 0;

            //recalculate text chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                //can join with next chunk(s)?
                while (i < chunks.Count - 1 && chunks[i].JoinChunk(chunks[i + 1], lMarg, this.Width, txtCMD.Font, measureWidth))
                    chunks.RemoveAt(i + 1);

                //need to split chunk?
                List<Chunk> newChunks = Chunk.Chunkify(chunks[i].ToString(), chunks[i].GetLink(), chunks[i].GetColor(), chunks[i].GetStrikeout(), lMarg, this.Width, txtCMD.Font, true, chunks[i].IsAbsNewline(), measureWidth, lineH);
                
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
            Hotkey.UnregisterHotKey(this);
        }

        private void formMain_KeyDown(object sender, KeyEventArgs e)
        {
            ctrlKey = e.Control;

            if (e.KeyCode == Keys.F5 || (txtSelection.Visible && e.KeyCode == Keys.Escape))
                toggleSelectMode();
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
            if (chunks.Count == 0)
                return;

            //scroll up/down 3 lines
            int scrollLineH = 0, yToScroll = 3 * lineH;

            if (e.Delta < 0)
                while (yToScroll > 0)
                {
                    //are there any more chunks?
                    if (chunkOffset == chunks.Count - 1)
                        break;

                    //get current chunk height
                    int h = chunks[chunkOffset].GetHeight();

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
                    //are there any more chunks?
                    if (chunkOffset == 0)
                        break;

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
            toggleSelectMode();
        }

        private void txtSelection_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            toggleSelectMode();
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

            if (twOutput)
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
                    case Keys.Escape:
                        twOutput = false;
                        setPrompt();

                        twitter.RemoveReadTweets();
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

        private void txtSelection_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
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

        private void txtSelection_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
                txtSelection.SelectAll();
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
            if (printingImgs.Count > 0 && lastChunk == printingImgs[0])
            {
                currImgChunkH += lineH;

                if (currImgChunkH >= chunks[printingImgs[0]].GetHeight())
                {
                    printingImgs.RemoveAt(0);
                    currImgChunkH = lineH;
                }
                else
                    return;
            }

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

                        if (printingImgs.Count > 0 && lastChunk < printingImgs[0])
                            printingImgs[0]--;
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