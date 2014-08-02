namespace Adjutant
{
    partial class formMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(formMain));
            this.timerDisableTopMost = new System.Windows.Forms.Timer(this.components);
            this.contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuOptions = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.txtCMD = new System.Windows.Forms.TextBox();
            this.timerPrint = new System.Windows.Forms.Timer(this.components);
            this.lblPrompt = new System.Windows.Forms.Label();
            this.trayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.timerAutohide = new System.Windows.Forms.Timer(this.components);
            this.timerShowHide = new System.Windows.Forms.Timer(this.components);
            this.timerPin = new System.Windows.Forms.Timer(this.components);
            this.timerMailCheck = new System.Windows.Forms.Timer(this.components);
            this.txtSelection = new System.Windows.Forms.TextBox();
            this.padMenus = new System.Windows.Forms.MenuStrip();
            this.padMenusFile = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusFileNew = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusFileOpen = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusFileSave = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusFileSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.padMenusFileExitPad = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusView = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewWordWrap = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewShowLineNumbers = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.padMenusViewSyntax = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxPlainText = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.padMenusViewSyntaxAssembly = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxCS = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxHTML = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxJavaScript = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxMSSQL = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxPostgreSQL = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxPython = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxVBScript = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusViewSyntaxXML = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusRun = new System.Windows.Forms.ToolStripMenuItem();
            this.padMenusFilename = new System.Windows.Forms.ToolStripMenuItem();
            this.openDiagPad = new System.Windows.Forms.OpenFileDialog();
            this.saveDiagPad = new System.Windows.Forms.SaveFileDialog();
            this.timerResizeWindow = new System.Windows.Forms.Timer(this.components);
            this.sciPad = new ScintillaNET.Scintilla();
            this.contextMenu.SuspendLayout();
            this.padMenus.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sciPad)).BeginInit();
            this.SuspendLayout();
            // 
            // timerDisableTopMost
            // 
            this.timerDisableTopMost.Interval = 250;
            this.timerDisableTopMost.Tick += new System.EventHandler(this.timerDisableTopMost_Tick);
            // 
            // contextMenu
            // 
            this.contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuOptions,
            this.menuExit});
            this.contextMenu.Name = "contextMenu";
            this.contextMenu.Size = new System.Drawing.Size(117, 48);
            // 
            // menuOptions
            // 
            this.menuOptions.Name = "menuOptions";
            this.menuOptions.Size = new System.Drawing.Size(116, 22);
            this.menuOptions.Text = "Options";
            this.menuOptions.Click += new System.EventHandler(this.menuOptions_Click);
            // 
            // menuExit
            // 
            this.menuExit.Name = "menuExit";
            this.menuExit.Size = new System.Drawing.Size(116, 22);
            this.menuExit.Text = "Exit";
            this.menuExit.Click += new System.EventHandler(this.menuExit_Click);
            // 
            // txtCMD
            // 
            this.txtCMD.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtCMD.Font = new System.Drawing.Font("Consolas", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCMD.Location = new System.Drawing.Point(0, 96);
            this.txtCMD.Name = "txtCMD";
            this.txtCMD.Size = new System.Drawing.Size(100, 13);
            this.txtCMD.TabIndex = 1;
            this.txtCMD.TabStop = false;
            this.txtCMD.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtCMD_KeyDown);
            this.txtCMD.MouseDown += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseDown);
            this.txtCMD.MouseMove += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseMove);
            this.txtCMD.MouseUp += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseUp);
            // 
            // timerPrint
            // 
            this.timerPrint.Interval = 1000;
            this.timerPrint.Tick += new System.EventHandler(this.timerPrint_Tick);
            // 
            // lblPrompt
            // 
            this.lblPrompt.Location = new System.Drawing.Point(0, 54);
            this.lblPrompt.Name = "lblPrompt";
            this.lblPrompt.Size = new System.Drawing.Size(10, 13);
            this.lblPrompt.TabIndex = 3;
            this.lblPrompt.Text = " ";
            this.lblPrompt.Visible = false;
            // 
            // trayIcon
            // 
            this.trayIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("trayIcon.Icon")));
            this.trayIcon.Text = "Adjutant";
            this.trayIcon.Visible = true;
            this.trayIcon.DoubleClick += new System.EventHandler(this.trayIcon_DoubleClick);
            // 
            // timerAutohide
            // 
            this.timerAutohide.Interval = 1000;
            this.timerAutohide.Tick += new System.EventHandler(this.timerAutohide_Tick);
            // 
            // timerShowHide
            // 
            this.timerShowHide.Interval = 20;
            this.timerShowHide.Tick += new System.EventHandler(this.timerShowHide_Tick);
            // 
            // timerPin
            // 
            this.timerPin.Interval = 5000;
            // 
            // timerMailCheck
            // 
            this.timerMailCheck.Tick += new System.EventHandler(this.timerMailCheck_Tick);
            // 
            // txtSelection
            // 
            this.txtSelection.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtSelection.Location = new System.Drawing.Point(0, 0);
            this.txtSelection.Multiline = true;
            this.txtSelection.Name = "txtSelection";
            this.txtSelection.ReadOnly = true;
            this.txtSelection.Size = new System.Drawing.Size(100, 20);
            this.txtSelection.TabIndex = 4;
            this.txtSelection.Visible = false;
            this.txtSelection.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.txtPad_MouseDoubleClick);
            this.txtSelection.MouseDown += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseDown);
            this.txtSelection.MouseMove += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseMove);
            this.txtSelection.MouseUp += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseUp);
            // 
            // padMenus
            // 
            this.padMenus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.padMenus.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.padMenusFile,
            this.padMenusView,
            this.padMenusRun,
            this.padMenusFilename});
            this.padMenus.Location = new System.Drawing.Point(0, 190);
            this.padMenus.Name = "padMenus";
            this.padMenus.Size = new System.Drawing.Size(219, 24);
            this.padMenus.TabIndex = 5;
            this.padMenus.Text = "menuStrip1";
            this.padMenus.Visible = false;
            this.padMenus.MouseDown += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseDown);
            this.padMenus.MouseMove += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseMove);
            this.padMenus.MouseUp += new System.Windows.Forms.MouseEventHandler(this.UIElement_MouseUp);
            // 
            // padMenusFile
            // 
            this.padMenusFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.padMenusFileNew,
            this.padMenusFileOpen,
            this.padMenusFileSave,
            this.padMenusFileSaveAs,
            this.toolStripSeparator1,
            this.padMenusFileExitPad});
            this.padMenusFile.Name = "padMenusFile";
            this.padMenusFile.Size = new System.Drawing.Size(37, 20);
            this.padMenusFile.Text = "File";
            // 
            // padMenusFileNew
            // 
            this.padMenusFileNew.Name = "padMenusFileNew";
            this.padMenusFileNew.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.padMenusFileNew.Size = new System.Drawing.Size(154, 22);
            this.padMenusFileNew.Text = "New";
            this.padMenusFileNew.Click += new System.EventHandler(this.padMenusFileNew_Click);
            // 
            // padMenusFileOpen
            // 
            this.padMenusFileOpen.Name = "padMenusFileOpen";
            this.padMenusFileOpen.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.padMenusFileOpen.Size = new System.Drawing.Size(154, 22);
            this.padMenusFileOpen.Text = "Open...";
            this.padMenusFileOpen.Click += new System.EventHandler(this.padMenusFileOpen_Click);
            // 
            // padMenusFileSave
            // 
            this.padMenusFileSave.Name = "padMenusFileSave";
            this.padMenusFileSave.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.padMenusFileSave.Size = new System.Drawing.Size(154, 22);
            this.padMenusFileSave.Text = "Save";
            this.padMenusFileSave.Click += new System.EventHandler(this.padMenusFileSave_Click);
            // 
            // padMenusFileSaveAs
            // 
            this.padMenusFileSaveAs.Name = "padMenusFileSaveAs";
            this.padMenusFileSaveAs.Size = new System.Drawing.Size(154, 22);
            this.padMenusFileSaveAs.Text = "Save As...";
            this.padMenusFileSaveAs.Click += new System.EventHandler(this.padMenusFileSaveAs_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(151, 6);
            // 
            // padMenusFileExitPad
            // 
            this.padMenusFileExitPad.Name = "padMenusFileExitPad";
            this.padMenusFileExitPad.Size = new System.Drawing.Size(154, 22);
            this.padMenusFileExitPad.Text = "Exit Pad";
            this.padMenusFileExitPad.Click += new System.EventHandler(this.padMenusFileExitPad_Click);
            // 
            // padMenusView
            // 
            this.padMenusView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.padMenusViewWordWrap,
            this.padMenusViewShowLineNumbers,
            this.toolStripSeparator2,
            this.padMenusViewSyntax});
            this.padMenusView.Name = "padMenusView";
            this.padMenusView.Size = new System.Drawing.Size(44, 20);
            this.padMenusView.Text = "View";
            // 
            // padMenusViewWordWrap
            // 
            this.padMenusViewWordWrap.Name = "padMenusViewWordWrap";
            this.padMenusViewWordWrap.Size = new System.Drawing.Size(176, 22);
            this.padMenusViewWordWrap.Text = "Word Wrap";
            this.padMenusViewWordWrap.Click += new System.EventHandler(this.padMenusViewWordWrap_Click);
            // 
            // padMenusViewShowLineNumbers
            // 
            this.padMenusViewShowLineNumbers.Name = "padMenusViewShowLineNumbers";
            this.padMenusViewShowLineNumbers.Size = new System.Drawing.Size(176, 22);
            this.padMenusViewShowLineNumbers.Text = "Show Line Numbers";
            this.padMenusViewShowLineNumbers.Click += new System.EventHandler(this.padMenusViewShowLineNumbers_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(173, 6);
            // 
            // padMenusViewSyntax
            // 
            this.padMenusViewSyntax.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.padMenusViewSyntaxPlainText,
            this.toolStripSeparator3,
            this.padMenusViewSyntaxAssembly,
            this.padMenusViewSyntaxCS,
            this.padMenusViewSyntaxHTML,
            this.padMenusViewSyntaxJavaScript,
            this.padMenusViewSyntaxMSSQL,
            this.padMenusViewSyntaxPostgreSQL,
            this.padMenusViewSyntaxPython,
            this.padMenusViewSyntaxVBScript,
            this.padMenusViewSyntaxXML});
            this.padMenusViewSyntax.Name = "padMenusViewSyntax";
            this.padMenusViewSyntax.Size = new System.Drawing.Size(176, 22);
            this.padMenusViewSyntax.Text = "Syntax";
            // 
            // padMenusViewSyntaxPlainText
            // 
            this.padMenusViewSyntaxPlainText.Name = "padMenusViewSyntaxPlainText";
            this.padMenusViewSyntaxPlainText.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxPlainText.Text = "Plain Text";
            this.padMenusViewSyntaxPlainText.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(133, 6);
            // 
            // padMenusViewSyntaxAssembly
            // 
            this.padMenusViewSyntaxAssembly.Name = "padMenusViewSyntaxAssembly";
            this.padMenusViewSyntaxAssembly.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxAssembly.Text = "Assembly";
            this.padMenusViewSyntaxAssembly.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxCS
            // 
            this.padMenusViewSyntaxCS.Name = "padMenusViewSyntaxCS";
            this.padMenusViewSyntaxCS.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxCS.Text = "C#";
            this.padMenusViewSyntaxCS.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxHTML
            // 
            this.padMenusViewSyntaxHTML.Name = "padMenusViewSyntaxHTML";
            this.padMenusViewSyntaxHTML.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxHTML.Text = "HTML";
            this.padMenusViewSyntaxHTML.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxJavaScript
            // 
            this.padMenusViewSyntaxJavaScript.Name = "padMenusViewSyntaxJavaScript";
            this.padMenusViewSyntaxJavaScript.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxJavaScript.Text = "JavaScript";
            this.padMenusViewSyntaxJavaScript.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxMSSQL
            // 
            this.padMenusViewSyntaxMSSQL.Name = "padMenusViewSyntaxMSSQL";
            this.padMenusViewSyntaxMSSQL.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxMSSQL.Text = "MS SQL";
            this.padMenusViewSyntaxMSSQL.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxPostgreSQL
            // 
            this.padMenusViewSyntaxPostgreSQL.Name = "padMenusViewSyntaxPostgreSQL";
            this.padMenusViewSyntaxPostgreSQL.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxPostgreSQL.Text = "Postgre SQL";
            this.padMenusViewSyntaxPostgreSQL.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxPython
            // 
            this.padMenusViewSyntaxPython.Name = "padMenusViewSyntaxPython";
            this.padMenusViewSyntaxPython.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxPython.Text = "Python";
            this.padMenusViewSyntaxPython.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxVBScript
            // 
            this.padMenusViewSyntaxVBScript.Name = "padMenusViewSyntaxVBScript";
            this.padMenusViewSyntaxVBScript.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxVBScript.Text = "VB Script";
            this.padMenusViewSyntaxVBScript.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusViewSyntaxXML
            // 
            this.padMenusViewSyntaxXML.Name = "padMenusViewSyntaxXML";
            this.padMenusViewSyntaxXML.Size = new System.Drawing.Size(136, 22);
            this.padMenusViewSyntaxXML.Text = "XML";
            this.padMenusViewSyntaxXML.Click += new System.EventHandler(this.padMenusViewSyntax_SelectLanguage_Click);
            // 
            // padMenusRun
            // 
            this.padMenusRun.Name = "padMenusRun";
            this.padMenusRun.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.padMenusRun.Size = new System.Drawing.Size(40, 20);
            this.padMenusRun.Text = "Run";
            this.padMenusRun.Click += new System.EventHandler(this.padMenusRun_Click);
            // 
            // padMenusFilename
            // 
            this.padMenusFilename.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.padMenusFilename.Name = "padMenusFilename";
            this.padMenusFilename.Size = new System.Drawing.Size(61, 20);
            this.padMenusFilename.Text = "Untitled";
            this.padMenusFilename.Click += new System.EventHandler(this.padMenusFilename_Click);
            // 
            // openDiagPad
            // 
            this.openDiagPad.Filter = "Text Documents|*.txt|All Files|*.*";
            // 
            // saveDiagPad
            // 
            this.saveDiagPad.Filter = "Text Documents|*.txt|All Files|*.*";
            // 
            // timerResizeWindow
            // 
            this.timerResizeWindow.Interval = 20;
            this.timerResizeWindow.Tick += new System.EventHandler(this.timerResizeWindow_Tick);
            // 
            // sciPad
            // 
            this.sciPad.Location = new System.Drawing.Point(0, 0);
            this.sciPad.Name = "sciPad";
            this.sciPad.Size = new System.Drawing.Size(132, 51);
            this.sciPad.Styles.BraceBad.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.BraceLight.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.CallTip.FontName = "Segoe UI\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.ControlChar.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.Default.BackColor = System.Drawing.SystemColors.Window;
            this.sciPad.Styles.Default.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.IndentGuide.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.LastPredefined.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.LineNumber.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.Styles.Max.FontName = "Verdana\0\0\0\0\0\0\0\0\0\0\0\0\0";
            this.sciPad.TabIndex = 6;
            this.sciPad.Visible = false;
            this.sciPad.KeyDown += new System.Windows.Forms.KeyEventHandler(this.sciPad_KeyDown);
            // 
            // formMain
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(219, 214);
            this.ContextMenuStrip = this.contextMenu;
            this.Controls.Add(this.padMenus);
            this.Controls.Add(this.txtSelection);
            this.Controls.Add(this.lblPrompt);
            this.Controls.Add(this.txtCMD);
            this.Controls.Add(this.sciPad);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MainMenuStrip = this.padMenus;
            this.MinimizeBox = false;
            this.Name = "formMain";
            this.Opacity = 0.33D;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Adjutant";
            this.Activated += new System.EventHandler(this.formMain_Activated);
            this.Deactivate += new System.EventHandler(this.formMain_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.formMain_FormClosing);
            this.Load += new System.EventHandler(this.formMain_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.formMain_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.formMain_DragEnter);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.formMain_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.formMain_KeyUp);
            this.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseDoubleClick);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseDown);
            this.MouseLeave += new System.EventHandler(this.formMain_MouseLeave);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseUp);
            this.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseWheel);
            this.Resize += new System.EventHandler(this.formMain_Resize);
            this.contextMenu.ResumeLayout(false);
            this.padMenus.ResumeLayout(false);
            this.padMenus.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sciPad)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Timer timerDisableTopMost;
        private System.Windows.Forms.ContextMenuStrip contextMenu;
        private System.Windows.Forms.ToolStripMenuItem menuExit;
        private System.Windows.Forms.TextBox txtCMD;
        private System.Windows.Forms.Timer timerPrint;
        private System.Windows.Forms.Label lblPrompt;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Windows.Forms.ToolStripMenuItem menuOptions;
        private System.Windows.Forms.Timer timerAutohide;
        private System.Windows.Forms.Timer timerShowHide;
        private System.Windows.Forms.Timer timerPin;
        private System.Windows.Forms.Timer timerMailCheck;
        private System.Windows.Forms.TextBox txtSelection;
        private System.Windows.Forms.MenuStrip padMenus;
        private System.Windows.Forms.ToolStripMenuItem padMenusFilename;
        private System.Windows.Forms.ToolStripMenuItem padMenusFile;
        private System.Windows.Forms.ToolStripMenuItem padMenusRun;
        private System.Windows.Forms.ToolStripMenuItem padMenusFileNew;
        private System.Windows.Forms.ToolStripMenuItem padMenusFileOpen;
        private System.Windows.Forms.ToolStripMenuItem padMenusFileSave;
        private System.Windows.Forms.ToolStripMenuItem padMenusFileSaveAs;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem padMenusFileExitPad;
        private System.Windows.Forms.OpenFileDialog openDiagPad;
        private System.Windows.Forms.SaveFileDialog saveDiagPad;
        private System.Windows.Forms.Timer timerResizeWindow;
        private ScintillaNET.Scintilla sciPad;
        private System.Windows.Forms.ToolStripMenuItem padMenusView;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewWordWrap;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewShowLineNumbers;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntax;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxAssembly;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxCS;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxHTML;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxJavaScript;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxMSSQL;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxPostgreSQL;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxPython;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxVBScript;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxXML;
        private System.Windows.Forms.ToolStripMenuItem padMenusViewSyntaxPlainText;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
    }
}

