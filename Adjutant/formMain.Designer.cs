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
            this.contextMenu.SuspendLayout();
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
            this.txtCMD.MouseDown += new System.Windows.Forms.MouseEventHandler(this.txtCMD_MouseDown);
            this.txtCMD.MouseMove += new System.Windows.Forms.MouseEventHandler(this.txtCMD_MouseMove);
            this.txtCMD.MouseUp += new System.Windows.Forms.MouseEventHandler(this.txtCMD_MouseUp);
            // 
            // timerPrint
            // 
            this.timerPrint.Interval = 1000;
            this.timerPrint.Tick += new System.EventHandler(this.timerPrint_Tick);
            // 
            // lblPrompt
            // 
            this.lblPrompt.AutoSize = true;
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
            // formMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(219, 214);
            this.ContextMenuStrip = this.contextMenu;
            this.Controls.Add(this.lblPrompt);
            this.Controls.Add(this.txtCMD);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
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
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.formMain_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.formMain_KeyUp);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseUp);
            this.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.formMain_MouseWheel);
            this.Resize += new System.EventHandler(this.formMain_Resize);
            this.contextMenu.ResumeLayout(false);
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
    }
}

