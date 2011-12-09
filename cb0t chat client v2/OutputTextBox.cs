using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Media;
using System.Text.RegularExpressions;

namespace cb0t_chat_client_v2
{
    class OutputTextBox : RichTextBox
    {
        private bool black_background;
        private bool left_button_down = false;
        private bool paused = false;
        private bool first_line = true;
        private List<PausedItem> pitems = new List<PausedItem>();

        public bool show_ips = true;

        public event ChannelList.ChannelClickedDelegate OnHashlinkClicked;
        public event Userlist.PMRequestDelegate OnCopyNameRequesting;

        public delegate void CATDelegate();
        public event CATDelegate OnCAT;

        public delegate void VCHandler(String id, bool is_pm);
        public delegate void VCDelHandler(String id, bool is_pm);
        public event VCDelHandler OnDeleteVC;
        public event VCHandler OnClickedVC;
        public event VCHandler OnSaveVC;

        public delegate void RadioHashlinkClickedHandler(String url);
        public event RadioHashlinkClickedHandler RadioHashlinkClicked;

        private bool is_pm;

        public OutputTextBox(bool is_main)
        {
            this.is_pm = !is_main;
            this.HideSelection = false;
            this.DetectUrls = true;
            this.Font = new Font("Verdana", 9.75F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            this.black_background = Settings.black_background;
            this.BackColor = this.black_background ? Color.Black : Color.White;
            this.ContextMenuStrip = new ContextMenuStrip();
            this.ContextMenuStrip.Items.Add("Clear screen");
            this.ContextMenuStrip.Items[0].Click += new EventHandler(this.OnClearScreen);
            this.ContextMenuStrip.Items.Add("Export text (plain)");
            this.ContextMenuStrip.Items[1].Click += new EventHandler(this.OnOpenInNotepad);
            this.ContextMenuStrip.Items.Add("Export text (rtf)");
            this.ContextMenuStrip.Items[2].Click += new EventHandler(this.OnOpenRTF);
            this.ContextMenuStrip.Items.Add("Copy all to clipboard");
            this.ContextMenuStrip.Items[3].Click += new EventHandler(this.OnCopyToClipBoard);
            this.ContextMenuStrip.Items.Add("Pause chat screen");
            this.ContextMenuStrip.Items[4].Click += new EventHandler(this.SetAutoScroll);

            if (is_main)
            {
                this.ContextMenuStrip.Items.Add("Close all sub tabs");
                this.ContextMenuStrip.Items[5].Click += new EventHandler(this.OnCloseAllTabs);
                
            }
            
            this.ContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.OnMenuOpening);
            this.ContextMenuStrip.Renderer = new ContextMenuRenderer();
            this.ContextMenuStrip.Opacity = 0.9;
            this.ContextMenuStrip.ForeColor = Color.Black;
        }

        private void OnCloseAllTabs(object sender, EventArgs e)
        {
            this.OnCAT();
        }

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.SelectedText.Length == 0)
                this.ContextMenuStrip.Items[3].Text = "Copy all to clipboard";
            else
                this.ContextMenuStrip.Items[3].Text = "Copy selected text to clipboard";

            this.ContextMenuStrip.Items[4].Text = this.paused ? "Unpause chat screen" : "Pause chat screen";
        }

        private void OnCopyToClipBoard(object sender, EventArgs e)
        {
            try
            {
                Clipboard.Clear();

                if (this.SelectedText.Length == 0)
                    Clipboard.SetText(this.Text.Replace("\n", "\r\n"));
                else
                    Clipboard.SetText(this.SelectedText.Replace("\n", "\r\n"));
            }
            catch { }
        }

        private void SetAutoScroll(object sender, EventArgs e)
        {
            if (this.paused)
            {
                this.paused = false;

                try
                {
                    PausedItem[] pi = this.pitems.ToArray();
                    this.pitems.Clear();

                    foreach (PausedItem i in pi)
                        this.OutBoxText(i.txt, i.first_color, i.user_name, i.allow_colors, i.user_font, i.start_with_line_break, null);
                }
                catch { }

                this.Announce("\x000314--- Chat screen unpaused");
            }
            else
            {
                this.Announce("\x000314--- Chat screen paused");
                this.paused = true;
            }
        }

        private void OnClearScreen(object sender, EventArgs e)
        {
            this.Clear();
        }

        private void OnOpenInNotepad(object sender, EventArgs e)
        {
            try
            {
                File.WriteAllLines(Settings.folder_path + "chatlog.txt", this.Lines, Encoding.UTF8);
                Process.Start("notepad.exe", Settings.folder_path + "chatlog.txt");
            }
            catch { }
        }

        private void OnOpenRTF(object sender, EventArgs e)
        {
            try
            {
                this.SaveFile(Settings.folder_path + "chatlog.rtf", RichTextBoxStreamType.RichText);
                Process.Start(Settings.folder_path + "chatlog.rtf");
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(HandleRef hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool LockWindowUpdate(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetScrollRange(IntPtr hWnd, int nBar, out int lpMinPos, out int lpMaxPos);

        [DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr hWnd, Orientation nBar);

        public bool IsScrolledToBottom
        {
            get
            {
                int scroll_position = GetScrollPos(this.Handle, Orientation.Vertical);
                int client_height = ScrollBar.FromChildHandle(this.Handle).ClientSize.Height;
                int scroll_vert_pos = client_height + scroll_position;
                int min = 0, max = 0;
                GetScrollRange(this.Handle, 1, out min, out max);
                return !(scroll_vert_pos <= (max - 10));
            }
        }

        private const String RTF_HEADER = "{\\rtf1\\ansi\\ansicpg1252\\deff0\\deflang1040{\\fonttbl{\\f0\\fswiss\\fprq2\\fcharset0";
        private const String RTF_COLORTBL1 = "{\\colortbl;\\red0\\green0\\blue0;\\red128\\green0\\blue0;\\red0\\green128\\blue0;\\red255\\green128\\blue0;\\red0\\green0\\blue128;\\red128\\green0\\blue128;\\red0\\green128\\blue128;\\red128\\green128\\blue128";
        private const String RTF_COLORTBL2 = ";\\red192\\green192\\blue192;\\red255\\green0\\blue0;\\red0\\green255\\blue0;\\red255\\green255\\blue0;\\red0\\green0\\blue255;\\red255\\green0\\blue255;\\red0\\green255\\blue255;\\red255\\green255\\blue255;}";
        private const String RTF_COLORTBL = RTF_COLORTBL1 + RTF_COLORTBL2;
        private const int WM_SETREDRAW = 0x000B;
        private const int WM_USER = 0x400;
        private const int EM_GETEVENTMASK = (WM_USER + 59);
        private const int EM_SETEVENTMASK = (WM_USER + 69);
        private const int SB_BOTTOM = 7;
        private const int WM_VSCROLL = 0x115;

        private delegate void AddAnnounceTextDelegate(String text);
        private delegate void AddSendTextDelegate(String name, String text);
        private delegate void AddScribbleDelegate(String text, Bitmap image);
        private delegate void JoinPartTextDelegate(UserObject userobj);
        private delegate void NudgeDelegate(String name, Random rnd);
        private delegate void SPTCDelegate();

        public void NudgeScreen(String name, Random rnd)
        {
            if (this.paused)
                return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new NudgeDelegate(this.NudgeScreen), name, rnd);
            }
            else
            {
                this.Announce("\x000314--- " + name + " has nudged you!");

                int x1, y1, x2, y2, z;
                x1 = this.Location.X;
                y1 = this.Location.Y;
                z = 0;

                MySoundEffects.PlayNudgeSound();

                while (z++ < 120)
                {
                    x2 = rnd.Next(x1 - 5, x1 + 5);
                    y2 = rnd.Next(y1 - 5, y1 + 5);
                    this.Location = new Point(x2, y2);
                }

                this.Location = new Point(x1, y1);
            }
        }

        public void FixScreen()
        {
            if (this.InvokeRequired)
                this.BeginInvoke(new SPTCDelegate(this.FixScreen));
            else
                SendMessage(this.Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);
        }

        private String TimeStamp()
        {
            return DateTime.Now.ToShortTimeString();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.C))
            {
                e.SuppressKeyPress = false;
            }
            else
            {
                e.SuppressKeyPress = true;
            }
        }

        protected override void OnLinkClicked(LinkClickedEventArgs e)
        {
            String str = e.LinkText;

            if (str.StartsWith("\\\\arlnk://"))
            {
                str = str.Substring(10);

                if (str.ToUpper().StartsWith("RADIO:"))
                {
                    str = str.Substring(6);

                    if (str.ToUpper().StartsWith("HTTP://"))
                        str = str.Substring(7);

                    String[] radio_args = str.Split(new String[] { ":" }, StringSplitOptions.RemoveEmptyEntries);

                    if (radio_args.Length == 2)
                    {
                        if (Helpers.IsIP(radio_args[0]))
                            if (Helpers.IsUshort(radio_args[1]))
                                this.RadioHashlinkClicked("http://" + radio_args[0] + ":" + radio_args[1]);
                    }
                    else if (radio_args.Length == 1)
                        if (Helpers.IsIP(radio_args[0]))
                            this.RadioHashlinkClicked("http://" + radio_args[0] + ":80");
                }
                else
                {
                    ChannelObject _obj = Hashlink.DecodeHashlink(str);

                    if (_obj != null)
                        this.OnHashlinkClicked(_obj);
                }
            }
            else if (str.StartsWith("\\\\voice_clip_#"))
            {
                String id = str.Replace("\\\\voice_clip_#", String.Empty);
                this.OnClickedVC(id, this.is_pm);
            }
            else if (str.StartsWith("\\\\save_#"))
            {
                String id = str.Replace("\\\\save_#", String.Empty);
                this.OnSaveVC(id, this.is_pm);
            }
            else
            {
                try
                {
                    Process.Start(str);
                }
                catch { }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x020A) // detect mouse-wheel before it's able to scroll
            {
                if (this.left_button_down) // we are zooming, disable vertical scroll
                {
                    if ((int)m.WParam > 0)
                        this.ZoomFactor += 0.1f;

                    if ((int)m.WParam < 0)
                        this.ZoomFactor -= 0.1f;

                    return;
                }
            }
            
            base.WndProc(ref m);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
                this.left_button_down = false;
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
                this.left_button_down = true;

            if (mevent.Button == MouseButtons.Middle)
            {
                int char_index = this.GetCharIndexFromPosition(mevent.Location);

                if (char_index <= 0)
                    return;

                String[] _split = this.Text.Split(new String[] { "\n" }, StringSplitOptions.None);

                int _start = 0;

                for (int i = 0; i < _split.Length; i++)
                {
                    if (char_index >= _start && char_index <= (_start + _split[i].Length))
                    {
                        String text = _split[i];

                        if (Settings.enable_timestamps)
                        {
                            int first_space = text.IndexOf(" ");

                            if (first_space == -1)
                                return;

                            text = text.Substring(first_space + 1);
                        }

                        int _gt = text.IndexOf(">");
                        int star = text.IndexOf("* ");

                        if (star == 0)
                        {
                            text = text.Substring(2);

                            int first_space = text.IndexOf(" ");

                            if (first_space < 2 || first_space > 20)
                                return;

                            text = text.Substring(0, first_space);
                            this.OnCopyNameRequesting(text);
                        }
                        else
                        {
                            if (_gt >= 2 && _gt <= 20)
                            {
                                text = text.Substring(0, _gt);
                                this.OnCopyNameRequesting(text);
                            }
                        }

                        return;
                    }

                    _start += (_split[i].Length + 1);
                }
            }
            else
            {
                base.OnMouseDown(mevent);
            }
        }

        public void PM(String name, String text, UserFont font, object custom_emotes)
        {
            if (font != null)
                this.OutBoxText(" " + name + ":", this.GetColorFromCode(font.name_col), null, false, null, true, null);
            else
                this.OutBoxText(" " + name + ":", this.black_background ? Color.Silver : Color.Black, null, false, font, true, null);

            this.OutBoxText("     " + text, this.black_background ? Color.Silver : Color.Black, null, true, font, true, custom_emotes);
        }

        private int cls_count = 0;

        public void Announce(String text)
        {
            if (Settings.ignore_cls)
            {
                if (text.Replace("\n", "").Replace("\r", "").Length == 0)
                {
                    if (this.cls_count++ > 6)
                        return;

                    int l_count = 0;

                    foreach (char c in text)
                        if (c == '\r' || c == '\n')
                            if (l_count++ > 20)
                                return;
                }
                else this.cls_count = 0;
            }

            this.OutBoxText(text, Color.Red, null, true);
        }

        public void Scribble(String info, Bitmap image)
        {
            if (this.paused)
                return;

            this.OutBoxText(info, Color.Gray, null, false);

            this.SelectionLength = 0;
            this.SelectionStart = this.Text.Length;
            this.SelectedRtf = "{\\rtf1 \\par}";
            this.SelectionLength = 0;
            this.SelectionStart = this.Text.Length;
            this.TrimLines();

            using (Graphics g = this.CreateGraphics())
                this.SelectedRtf = OutputTextBoxEmoticons.GetRTFScribble(image, g);

            this.SelectionStart = this.Text.Length;
            this.SelectionLength = 0;
        }

        public void SentAVoiceClip()
        {
            this.Announce("\x000314--- your voice clip has recorded and is now being sent...");
        }

        public void VoiceClipCancel()
        {
            this.Announce("\x000314--- your voice clip was cancelled");
        }

        public void ReceivedAVoiceClip(String text)
        {
            this.Announce("\x000314--- \\\\" + text);
        }

        private delegate void OutBoxScribbleHandler(String info, byte[] image);
        public void Scribble(String info, byte[] image)
        {
            if (this.paused)
                return;

            if (this.InvokeRequired)
                this.BeginInvoke(new OutBoxScribbleHandler(this.Scribble), info, image);
            else
            {
                this.OutBoxText(info, Color.Gray, null, false);

                this.SelectionLength = 0;
                this.SelectionStart = this.Text.Length;
                this.SelectedRtf = "{\\rtf1 \\par}";
                this.SelectionLength = 0;
                this.SelectionStart = this.Text.Length;

                this.TrimLines();

                using (MemoryStream ms = new MemoryStream(image))
                {
                    using (Bitmap b = new Bitmap(ms))
                    {
                    //    Clipboard.SetImage(b);
                    //    this.Paste(); // primative
                        using (Graphics g = this.CreateGraphics())
                            this.SelectedRtf = OutputTextBoxEmoticons.GetRTFScribble(b, g);
                    }
                }

                this.SelectionStart = this.Text.Length;
                this.SelectionLength = 0;
            }
        }

        public void Server(String text)
        {
            this.OutBoxText(text, this.black_background ? Color.Gray : Color.Navy, null, true);
        }

        public void Public(String name, String text)
        {
            this.OutBoxText(text, this.black_background ? Color.White : Color.Blue, name, true);
        }

        public void Emote(String name, String text)
        {
            this.OutBoxText("* " + name + " " + text, this.black_background ? Color.Fuchsia : Color.Purple, null, false);
        }

        public void Public(String name, String text, UserFont font, object custom_emotes)
        {
            this.OutBoxText(text, this.black_background ? Color.White : Color.Blue, name, true, font, true, custom_emotes);
        }

        public void Emote(String name, String text, UserFont font, object custom_emotes)
        {
            this.OutBoxText("* " + name + " " + text, this.black_background ? Color.Fuchsia : Color.Purple, null, false, font, true, custom_emotes);
        }

        public void Join(UserObject userobj)
        {
            if (this.show_ips)
                this.OutBoxText(userobj.name + " [" + userobj.externalIp + "] sharing " + userobj.files + " files, has joined", this.black_background ? Color.Lime : Color.Green, null, false);
            else
                this.OutBoxText(userobj.name + " sharing " + userobj.files + " files, has joined", this.black_background ? Color.Lime : Color.Green, null, false);
        }

        public void Part(UserObject userobj)
        {
            if (this.show_ips)
                this.OutBoxText(userobj.name + " [" + userobj.externalIp + "] has parted", Color.Orange, null, false);
            else
                this.OutBoxText(userobj.name + " has parted", Color.Orange, null, false);
        }

        public void OutBoxText(String txt, Color first_color, String user_name, bool allow_colors)
        {
            this.OutBoxText(txt, first_color, user_name, allow_colors, null, true, null);
        }

        private delegate void OutBoxTextHandler(String text, Color first_color, String user_name, bool allow_colors, UserFont user_font, bool start_with_line_break, object custom_emotes);
        public void OutBoxText(String txt, Color first_color, String user_name, bool allow_colors, UserFont user_font, bool start_with_line_break, object custom_emotes)
        {
            if (this.paused)
            {
                this.pitems.Add(new PausedItem(txt, first_color, user_name, allow_colors, user_font, start_with_line_break));
                return;
            }

            if (this.InvokeRequired)
                this.BeginInvoke(new OutBoxTextHandler(this.OutBoxText), txt, first_color, user_name, allow_colors, user_font, start_with_line_break, custom_emotes);
            else
            {
                // bad chars
                StringBuilder text = new StringBuilder();
                text.Append(txt);
                text.Replace("\r\n", "\r");
                text.Replace("\n", "\r");
                text.Replace("", "");
                text.Replace("]̽", "");
                text.Replace(" ̽", "");
                text.Replace("͊", "");
                text.Replace("]͊", "");
                text.Replace("͠", "");
                text.Replace("̶", "");
                text.Replace("̅", "");

                CEmoteItem[] user_emotes = new CEmoteItem[] { };

                if (custom_emotes != null)
                    user_emotes = (CEmoteItem[])custom_emotes;

                String font_name = user_font == null ? Settings.font_name : user_font.name;
                int font_size = user_font == null ? Settings.font_size : user_font.size;
                bool custom_colors = false;

                if (user_font != null)
                    if (user_font.text_col > -1)
                        if (user_font.name_col > -1)
                            custom_colors = true;

                if (!Settings.font_list.Contains(font_name))
                {
                    font_name = Settings.font_name;
                    font_size = Settings.font_size;
                }

                int emote_count = 0;

                StringBuilder str = new StringBuilder();
                str.Append("\\fs" + (font_size * 2) + this.ColorToRTF(this.black_background ? Color.Yellow : Color.Black, true) + this.ColorToRTF(this.black_background ? Color.Black : Color.White, false));

                if (Settings.enable_timestamps)
                    str.Append(this.TimeStamp() + " ");

                if (!String.IsNullOrEmpty(user_name))
                {
                    if (custom_colors)
                    {
                        str.Append("\\cf0" + this.ColorToRTF(this.GetColorFromCode(user_font.name_col), true));

                        if (this.black_background && user_font.name_col == 1)
                            str.Append("\\highlight0" + this.ColorToRTF(Color.White, false));
                    }

                    foreach (char c in (user_name + "> ").ToCharArray())
                        str.Append("\\u" + ((int)c) + "?");
                }

                if (custom_colors)
                {
                    str.Append("\\cf0" + this.ColorToRTF(this.GetColorFromCode(user_font.text_col), true));

                    if (this.black_background && user_font.text_col == 1)
                        str.Append("\\highlight0" + this.ColorToRTF(Color.White, false));
                }
                else str.Append("\\cf0" + this.ColorToRTF(first_color, true));

                bool bold = false, italic = false, underline = false;
                Color fore_color = first_color, back_color = this.black_background ? Color.Black : Color.White;
                char[] letters = text.ToString().ToCharArray();
                int color_finder;

                using (Graphics richtextbox = this.CreateGraphics())
                {
                    for (int i = 0; i < letters.Length; i++)
                    {
                        switch (letters[i])
                        {
                            case '\x0006': // bold
                                if (allow_colors)
                                {
                                    bold = !bold;
                                    str.Append(bold ? "\\b" : "\\b0");
                                }
                                break;

                            case '\x0007': // underline
                                if (allow_colors)
                                {
                                    underline = !underline;
                                    str.Append(underline ? "\\ul" : "\\ul0");
                                }
                                break;

                            case '\x0009': // italic
                                if (allow_colors)
                                {
                                    italic = !italic;
                                    str.Append(italic ? "\\i" : "\\i0");
                                }
                                break;

                            case '\x0003': // fore color
                                if (allow_colors)
                                {
                                    if (letters.Length >= (i + 3))
                                    {
                                        if (int.TryParse(new String(new char[] { text[i + 1], text[i + 2] }), out color_finder))
                                        {
                                            fore_color = this.GetColorFromCode(color_finder);
                                            str.Append("\\cf0" + this.ColorToRTF(fore_color, true));
                                            i += 2;
                                            break;
                                        }
                                        else goto default;
                                    }
                                    else goto default;
                                }
                                else goto default;

                            case '\x0005': // back color
                                if (allow_colors)
                                {
                                    if (letters.Length >= (i + 3))
                                    {
                                        if (int.TryParse(new String(new char[] { text[i + 1], text[i + 2] }), out color_finder))
                                        {
                                            back_color = this.GetColorFromCode(color_finder);
                                            str.Append("\\highlight0" + this.ColorToRTF(back_color, false));
                                            i += 2;
                                            break;
                                        }
                                        else goto default;
                                    }
                                    else goto default;
                                }
                                else goto default;

                            case '+':
                            case '(':
                            case ':':
                            case ';': // emoticons
                                if (Settings.enable_emoticons)
                                {
                                    int emote_index = EmoticonFinder.GetRTFEmoticonFromKeyboardShortcut(text, i);

                                    if (emote_index > -1 && emote_count++ < 8)
                                    {
                                        str.Append(OutputTextBoxEmoticons.GetRTFEmoticon(emote_index, back_color, richtextbox));
                                        i += (EmoticonFinder.last_emote_length - 1);
                                        break;
                                    }
                                    else goto default;
                                }
                                else goto default;

                            default: // text
                                bool was_user_emote = false;

                                for (int x = 0; x < user_emotes.Length; x++)
                                {
                                    if (user_emotes[x].Image != null)
                                    {
                                        if (user_emotes[x].Shortcut.StartsWith(letters[i].ToString())) // could be
                                        {
                                            String remainder = text.ToString().Substring(i);

                                            if (remainder.StartsWith(user_emotes[x].Shortcut))
                                            {
                                                if (emote_count++ < 8)
                                                {
                                                    if (user_emotes[x].Rtf == null)
                                                        user_emotes[x].Rtf = OutputTextBoxEmoticons.GetRTFCustomEmote(user_emotes[x], richtextbox);

                                                    if (user_emotes[x].Rtf != null)
                                                    {
                                                        str.Append(user_emotes[x].Rtf);
                                                        was_user_emote = true;
                                                        i += (user_emotes[x].Shortcut.Length - 1);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (!was_user_emote)
                                    str.Append("\\u" + ((int)letters[i]) + "?");

                                break;
                        }
                    }
                }

                if (underline) str.Append("\\ul0");
                if (italic) str.Append("\\i0");
                if (bold) str.Append("\\b0");

                str.Append("\\highlight0\\cf0}");

                this.SelectionLength = 0;
                this.SelectionStart = this.Text.Length;
                this.TrimLines();

                if (this.first_line)
                {
                    this.first_line = false;
                    this.SelectedRtf = RTF_HEADER + " " + font_name + ";}}" + RTF_COLORTBL + str;
                    this.SelectionStart = this.Text.Length;
                    this.SelectionLength = 0;
                }
                else
                {
                    if (start_with_line_break)
                    {
                        this.SelectedRtf = "{\\rtf1 \\par}";
                        this.SelectionStart = this.Text.Length;
                        this.SelectionLength = 0;
                    }

                    this.SelectedRtf = RTF_HEADER + " " + font_name + ";}}" + RTF_COLORTBL + str;
                    this.SelectionStart = this.Text.Length;
                    this.SelectionLength = 0;
                }

                text = null;
                str = null;
            }
        }

        private void TrimLines()
        {
            if (this.Lines.Length > 500)
            {
                SendMessage(this.Handle, WM_SETREDRAW, 0, IntPtr.Zero);
                IntPtr eventMask = SendMessage(this.Handle, EM_GETEVENTMASK, 0, IntPtr.Zero);
                bool should_gc = false;

                while (this.Lines.Length > 300)
                {
                    int i = this.Text.IndexOf("\n");

                    if (i == -1)
                        break;

                    String line_text = this.Text.Substring(0, i);

                    if (Regex.Match(line_text, "(.*?)\\-\\-\\-").Success) // starts with "---" or "xx:xx xx ---"
                    {
                        line_text = Regex.Replace(line_text, "(.*?)\\-\\-\\-", "---"); // strip timestamp

                        if (line_text.StartsWith("--- \\\\voice_clip_#"))
                        {
                            line_text = line_text.Replace("--- \\\\voice_clip_#", String.Empty).Split(new String[] { " " }, StringSplitOptions.None)[0];
                            this.OnDeleteVC(line_text, this.is_pm);
                            should_gc = true;
                        }
                    }

                    this.Select(0, (i + 1));
                    this.SelectedText = String.Empty;
                }

                SendMessage(this.Handle, EM_SETEVENTMASK, 0, eventMask);
                SendMessage(this.Handle, WM_SETREDRAW, 1, IntPtr.Zero);

                this.SelectionLength = 0;
                this.SelectionStart = this.Text.Length;

                if (should_gc)
                    GC.Collect();
            }
        }

        private String ColorToRTF(Color color, bool foreground)
        {
            if (color == Color.White) return foreground ? "\\cf16 " : "\\highlight16 "; //16
            if (color == Color.Black) return foreground ? "\\cf1 " : "\\highlight1 "; //1
            if (color == Color.Navy) return foreground ? "\\cf5 " : "\\highlight5 "; //5
            if (color == Color.Green) return foreground ? "\\cf3 " : "\\highlight3 "; //3
            if (color == Color.Red) return foreground ? "\\cf10 " : "\\highlight10 "; //10
            if (color == Color.Maroon) return foreground ? "\\cf2 " : "\\highlight2 "; //2
            if (color == Color.Purple) return foreground ? "\\cf6 " : "\\highlight6 "; //6
            if (color == Color.Orange) return foreground ? "\\cf4 " : "\\highlight4 "; //4
            if (color == Color.Yellow) return foreground ? "\\cf12 " : "\\highlight12 "; //12
            if (color == Color.Lime) return foreground ? "\\cf11 " : "\\highlight11 "; //11
            if (color == Color.Teal) return foreground ? "\\cf7 " : "\\highlight7 "; //7
            if (color == Color.Aqua) return foreground ? "\\cf15 " : "\\highlight15 "; //15
            if (color == Color.Blue) return foreground ? "\\cf13 " : "\\highlight13 "; //13
            if (color == Color.Fuchsia) return foreground ? "\\cf14 " : "\\highlight14 "; //14
            if (color == Color.Gray) return foreground ? "\\cf8 " : "\\highlight8 "; //8
            if (color == Color.Silver) return foreground ? "\\cf9 " : "\\highlight9 "; //9
            return String.Empty;
        }

        private Color GetColorFromCode(int code)
        {
            switch (code)
            {
                case 1: return Color.Black;
                case 2: return Color.Navy;
                case 3: return Color.Green;
                case 4: return Color.Red;
                case 5: return Color.Maroon;
                case 6: return Color.Purple;
                case 7: return Color.Orange;
                case 8: return Color.Yellow;
                case 9: return Color.Lime;
                case 10: return Color.Teal;
                case 11: return Color.Aqua;
                case 12: return Color.Blue;
                case 13: return Color.Fuchsia;
                case 14: return Color.Gray;
                case 15: return Color.Silver;
                default: return Color.White;
            }
        }
    }

    class PausedItem
    {
        public String txt;
        public Color first_color;
        public String user_name;
        public bool allow_colors;
        public UserFont user_font;
        public bool start_with_line_break;

        public PausedItem(String txt, Color first_color, String user_name, bool allow_colors, UserFont user_font, bool start_with_line_break)
        {
            this.txt = txt;
            this.first_color = first_color;
            this.user_name = user_name;
            this.allow_colors = allow_colors;
            this.user_font = user_font;
            this.start_with_line_break = start_with_line_break;
        }
    }
}
