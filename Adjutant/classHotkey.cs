using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Adjutant
{
    class Hotkey
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static int MOD_ALT = 0x1;
        public static int MOD_CONTROL = 0x2;
        public static int MOD_SHIFT = 0x4;
        public static int MOD_WIN = 0x8;
        public static int WM_HOTKEY = 0x312;
        private static int keyId, launcherKeyId;


        public static void RegisterHotKey(Form f, int hotkey, bool ctrl, bool alt, bool shift, bool launcher)
        {
            int modifiers = 0;

            if (ctrl)
                modifiers = modifiers | MOD_CONTROL;
            if (alt)
                modifiers = modifiers | MOD_ALT;
            if (shift)
                modifiers = modifiers | MOD_SHIFT;

            if (!launcher)
            {
                keyId = f.GetHashCode();
                RegisterHotKey((IntPtr)f.Handle, keyId, modifiers, hotkey);
            }
            else
            {
                launcherKeyId = f.GetHashCode() + 1;
                RegisterHotKey((IntPtr)f.Handle, launcherKeyId, modifiers, hotkey);
            }
        }
        
        public static void UnregisterHotKey(Form f, bool launcher)
        {
            try
            {
                if (!launcher)
                    UnregisterHotKey(f.Handle, keyId);
                else
                    UnregisterHotKey(f.Handle, launcherKeyId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public static string HotkeyToString(int hotkey, bool ctrl, bool alt, bool shift)
        {
            string s = "";

            if (ctrl)
                s += "CTRL+";
            if (alt)
                s += "ALT+";
            if (shift)
                s += "SHIFT+";

            return s + (char)hotkey;
        }

        public static void StringToHotkey(string s, out int hotkey, out bool ctrl, out  bool alt, out  bool shift)
        {
            string[] els = s.Split(new string[] { "+" }, StringSplitOptions.RemoveEmptyEntries);

            ctrl = els.Contains("CTRL");
            alt = els.Contains("ALT");
            shift = els.Contains("SHIFT");
            
            int last = els.Length - 1;
            if (last != -1 && els[last] != "CTRL" && els[last] != "ALT" && els[last] != "SHIFT")
                hotkey = els[last][0];
            else
                hotkey = 0;
        }
    }
}
