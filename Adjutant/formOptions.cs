using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Adjutant
{
    public partial class formOptions : Form
    {
        public formMain main;
        public bool starting;


        void checkForChanges(object sender, EventArgs e)
        {
            checkForChanges();   
        }

        void checkForChanges()
        {
            if (starting)
                return;

            bool changed = false;

            changed |= compareValueToTag(numX);
            changed |= compareValueToTag(numY);
            changed |= compareValueToTag(numW);
            changed |= compareValueToTag(numMinH);
            changed |= compareValueToTag(numMaxH);
            changed |= compareValueToTag(txtHotkey);
            changed |= compareValueToTag(numAutoHideDelay);
            changed |= compareValueToTag(comboHideStyle);
            changed |= compareValueToTag(numOpacityActive);
            changed |= compareValueToTag(numOpacityPassive);
            changed |= compareValueToTag(picBackColor);

            changed |= compareValueToTag(comboFont);
            changed |= compareValueToTag(numFontSize);
            changed |= compareValueToTag(chkBold);
            changed |= compareValueToTag(chkItalic);
            changed |= compareValueToTag(picTextColor);

            changed |= compareValueToTag(txtStartDir);
            changed |= compareValueToTag(numPrintDelay);
            changed |= compareValueToTag(numPrintAtOnce);
            changed |= compareValueToTag(chkPrompt);
            changed |= compareValueToTag(chkBlankLine);
            changed |= compareValueToTag(chkEcho);
            changed |= compareValueToTag(picEchoColor);
            changed |= compareValueToTag(picErrorColor);

            changed |= compareValueToTag(txtTodoDir);
            changed |= compareValueToTag(checkTodoHideDone);
            changed |= compareValueToTag(checkTodoAutoTransfer);
            changed |= compareValueToTag(picTodoMiscColor);
            changed |= compareValueToTag(picTodoItemColor);
            changed |= compareValueToTag(picTodoDoneColor);

            changed |= compareValueToTag(checkTwCountOnNewTweet);
            changed |= compareValueToTag(checkTwCountOnFocus);
            changed |= compareValueToTag(numTwCountMinPeriod);
            changed |= compareValueToTag(picTwUsernameColor);
            changed |= compareValueToTag(picTwMiscColor);
            changed |= compareValueToTag(picTwTweetColor);
            changed |= compareValueToTag(picTwLinkColor);
            changed |= compareValueToTag(picTwTimestampColor);
            changed |= compareValueToTag(picTwCountColor);

            changed |= compareValueToTag(txtUser);
            changed |= compareValueToTag(txtPass);
            changed |= compareValueToTag(checkMailCountOnFocus);
            changed |= compareValueToTag(checkMailCountOnNewMail);
            changed |= compareValueToTag(numMailCheckPeriod);
            changed |= compareValueToTag(picMailCountColor);
            changed |= compareValueToTag(picMailHeaderColor);
            changed |= compareValueToTag(picMailSummaryColor);

            buttSave.Visible = changed;
        }

        bool compareValueToTag(NumericUpDown num)
        {
            return num.Value != (int)num.Tag;
        }

        bool compareValueToTag(TextBox txt)
        {
            return txt.Text != txt.Tag.ToString();
        }

        bool compareValueToTag(ComboBox combo)
        {
            return combo.Text != combo.Tag.ToString();
        }

        bool compareValueToTag(CheckBox chk)
        {
            return chk.Checked != (bool)chk.Tag;
        }

        bool compareValueToTag(PictureBox pic)
        {
            return pic.BackColor.ToArgb() != ((Color)pic.Tag).ToArgb();
        }

        void pickColor(PictureBox pic)
        {
            if (diagColor.ShowDialog() != System.Windows.Forms.DialogResult.Cancel)
            {
                pic.BackColor = diagColor.Color;
                checkForChanges();
            }
        }

        void browseDir(TextBox txt)
        {
            if (Directory.Exists(txt.Text))
                folderDiag.SelectedPath = txt.Text;

            if (folderDiag.ShowDialog() != System.Windows.Forms.DialogResult.Cancel)
            {
                txt.Text = folderDiag.SelectedPath;
                checkForChanges();
            }
        }

        void fontPreview()
        {
            FontStyle style = FontStyle.Regular;
            if (chkBold.Checked && chkItalic.Checked)
                style = FontStyle.Bold | FontStyle.Italic;
            else if (chkBold.Checked)
                style = FontStyle.Bold;
            else if (chkItalic.Checked)
                style = FontStyle.Italic;

            lblFontPreview.Font = new Font(comboFont.Text, (float)numFontSize.Value, style);

            lblFontPreview.ForeColor = picTextColor.BackColor;
            if (lblFontPreview.ForeColor.ToArgb() == -1)
                lblFontPreview.BackColor = Color.Black;
            else
                lblFontPreview.BackColor = Color.White;
        }


        public formOptions()
        {
            InitializeComponent();
        }

        private void formOptions_Load(object sender, EventArgs e)
        {
            fontPreview();
        }
        
        private void trackOpacityActive_Scroll(object sender, EventArgs e)
        {
            numOpacityActive.Value = trackOpacityActive.Value;
            checkForChanges();
        }

        private void numOpacityActive_ValueChanged(object sender, EventArgs e)
        {
            trackOpacityActive.Value = (int)numOpacityActive.Value;
            checkForChanges();
        }

        private void trackOpacityPassive_Scroll(object sender, EventArgs e)
        {
            numOpacityPassive.Value = trackOpacityPassive.Value;
            checkForChanges();
        }

        private void numOpacityPassive_ValueChanged(object sender, EventArgs e)
        {
            trackOpacityPassive.Value = (int)numOpacityPassive.Value;
            checkForChanges();
        }

        private void buttPickBackColor_Click(object sender, EventArgs e)
        {
            pickColor(picBackColor);
        }

        private void picBackColor_Click(object sender, EventArgs e)
        {
            pickColor(picBackColor);
        }
        
        private void comboFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            checkForChanges();
            fontPreview();
        }

        private void numFontSize_ValueChanged(object sender, EventArgs e)
        {
            checkForChanges();
            fontPreview();
        }

        private void chkBold_CheckedChanged(object sender, EventArgs e)
        {
            checkForChanges();
            fontPreview();
        }

        private void chkItalic_CheckedChanged(object sender, EventArgs e)
        {
            checkForChanges();
            fontPreview();
        }

        private void buttEditFont_Click(object sender, EventArgs e)
        {
            diagFont.MinSize = 6;
            diagFont.MaxSize = 42;
            diagFont.Font = lblFontPreview.Font;
            diagFont.Color = picTextColor.BackColor;
            diagFont.ShowColor = true;

            diagFont.ShowDialog();

            comboFont.Text = diagFont.Font.Name;
            numFontSize.Value = (int)diagFont.Font.Size;
            chkBold.Checked = diagFont.Font.Bold;
            chkItalic.Checked = diagFont.Font.Italic;
            picTextColor.BackColor = diagFont.Color;

            fontPreview();
        }

        private void buttPickTextColor_Click(object sender, EventArgs e)
        {
            pickColor(picTextColor);
            fontPreview();
        }

        private void picTextColor_Click(object sender, EventArgs e)
        {
            pickColor(picTextColor);
        }

        private void txtStartDir_TextChanged(object sender, EventArgs e)
        {
            
        }

        private void numPrintDelay_ValueChanged(object sender, EventArgs e)
        {
            checkForChanges();
        }

        private void numPrintAtOnce_ValueChanged(object sender, EventArgs e)
        {
            checkForChanges();
        }

        private void chkPrompt_CheckedChanged(object sender, EventArgs e)
        {
            checkForChanges();
        }

        private void buttSave_Click(object sender, EventArgs e)
        {
            //check if folders exist
            if (!Directory.Exists(txtStartDir.Text))
                MessageBox.Show("Please enter an existing folder path.", "Start directory does not exist!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (!Directory.Exists(txtTodoDir.Text))
                MessageBox.Show("Please enter an existing folder path.", "Todo directory does not exist!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                main.UpdateOptions(this);
                this.Close();
            }
        }

        private void buttPickTwUsernameColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwUsernameColor);
        }

        private void picTwUsernameColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwUsernameColor);
        }

        private void buttPickTwMiscColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwMiscColor);
        }

        private void picTwMiscColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwMiscColor);
        }

        private void buttPickTwTweetColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwTweetColor);
        }

        private void picTwTweetColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwTweetColor);
        }

        private void buttPickTwLinkColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwLinkColor);
        }

        private void picTwLinkColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwLinkColor);
        }

        private void buttPickTwTimestampColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwTimestampColor);
        }

        private void picTwTimestampColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwTimestampColor);
        }

        private void buttPickTwCountColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwCountColor);
        }

        private void picTwCountColor_Click(object sender, EventArgs e)
        {
            pickColor(picTwCountColor);
        }

        private void buttPickErrorColor_Click(object sender, EventArgs e)
        {
            pickColor(picErrorColor);
        }

        private void picErrorColor_Click(object sender, EventArgs e)
        {
            pickColor(picErrorColor);
        }

        private void buttPickTodoMiscColor_Click(object sender, EventArgs e)
        {
            pickColor(picTodoMiscColor);
        }

        private void picTodoMiscColor_Click(object sender, EventArgs e)
        {
            pickColor(picTodoMiscColor);
        }

        private void buttPickTodoItemColor_Click(object sender, EventArgs e)
        {
            pickColor(picTodoItemColor);
        }

        private void picTodoItemColor_Click(object sender, EventArgs e)
        {
            pickColor(picTodoItemColor);
        }

        private void buttPickTodoDoneColor_Click(object sender, EventArgs e)
        {
            pickColor(picTodoDoneColor);
        }

        private void picTodoDoneColor_Click(object sender, EventArgs e)
        {
            pickColor(picTodoDoneColor);
        }

        private void buttBrowseForStartDir_Click(object sender, EventArgs e)
        {
            browseDir(txtStartDir);
        }

        private void buttBrowseForTodoDir_Click(object sender, EventArgs e)
        {
            browseDir(txtTodoDir);
        }

        private void txtStartDir_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                checkForChanges();
        }

        private void txtTodoDir_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                checkForChanges();
        }

        private void chkEcho_CheckedChanged(object sender, EventArgs e)
        {
            checkForChanges();
        }

        private void buttPickEchoColor_Click(object sender, EventArgs e)
        {
            pickColor(picEchoColor);
        }

        private void chkBlankLine_CheckedChanged(object sender, EventArgs e)
        {
            checkForChanges();
        }

        private void txtHotkey_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Menu && e.KeyCode != Keys.ShiftKey)
            {
                txtHotkey.Text = Hotkey.HotkeyToString(e.KeyValue, e.Control, e.Alt, e.Shift);
                e.SuppressKeyPress = true;

                checkForChanges();
            }
        }
        
        private void buttPickMailCountColor_Click(object sender, EventArgs e)
        {
            pickColor(picMailCountColor);
        }

        private void buttPickMailHeaderColor_Click(object sender, EventArgs e)
        {
            pickColor(picMailHeaderColor);
        }

        private void buttPickMailSummaryColor_Click(object sender, EventArgs e)
        {
            pickColor(picMailSummaryColor);
        }

        private void txtUser_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                checkForChanges();
        }
    }
}
