using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Drawing.Text;

namespace Q9CS
{

    public enum Q9command
    {
        cancel,
        prev,
        next,
        homo,
        openclose,
        relate,
        shortcut,
        sc,
    }

    public class IniFile
    {
        readonly string filePath;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public IniFile(string filePath)
        {
            //var dir = Path.GetDirectoryName(filePath);
            var dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir ?? throw new InvalidOperationException("Invalid file path"));
            }

            this.filePath = Path.Combine(dir, filePath);
        }

        public bool MakeExcite()
        {

            // Check if the file exists, and if not, create an empty file
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
                return false;
            }
            return true;
        }

        public void Write(string section, string key, int value)
        {
            long result = WritePrivateProfileString(section, key, value.ToString(), filePath);
            Debug.WriteLine($"{result},{filePath},{key}");
        }

        public int ReadInt(string section, string key, int defaultValue)
        {
            StringBuilder SB = new StringBuilder(255);
            _ = GetPrivateProfileString(section, key, defaultValue.ToString(), SB, 255, filePath);
            return int.TryParse(SB.ToString(), out int i) ? i : defaultValue;
        }
    }


    public partial class Q9Form : Form
    {

        private static readonly string StartupKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private static readonly string StartupValue = "Q9CS";


        private readonly Q9Core core;
        private readonly List<Button> buttons = new List<Button> { };

        private readonly Image[] images = new Image[120];

        private bool active = true;
        private bool sc_output = false;
        private bool use_numpad = true;

        private readonly IniFile ini = new IniFile("tq9_settings.ini");
        private readonly Dictionary<string, int> Keys = new Dictionary<string, int>();
        private readonly Dictionary<string, int> altKeys = new Dictionary<string, int>();

        public void Q9Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Save size and position
            ini.Write("Window", "Left", Left);
            ini.Write("Window", "Top", Top);
            ini.Write("Window", "BoxSize", currBoxSize);
        }

        //

        public Q9Form()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.Manual;
            ControlBox = false;

            Image[] bigImages = new Image[11];
            // load 0.bmp ~ 9.bmp
            for (int i = 0; i <= 10; i++)
            {
                bigImages[i] = Image.FromFile($"files/img/{i}.bmp");
            }
            int smallImageSize = 51;
            //preload image
            for (int i = 0; i <= 10; i++)
            {
                //split bigImages[i] to 9 images (3x3), big image size is 154x154
                for (int j = 1; j <= 9; j++)
                {
                    int x = (j - 1) % 3;
                    int y = (j - 1) / 3;
                    // flip j (123<>789) and set to _j
                    int _j = (j - 1) % 3 + (2 - (j - 1) / 3) * 3 + 1;
                    int index = i * 10 + _j;
                    images[index] = new Bitmap(smallImageSize, smallImageSize);
                    using (Graphics g = Graphics.FromImage(images[index]))
                    {
                        g.DrawImage(
                            bigImages[i], 
                            new Rectangle(0, 0, smallImageSize, smallImageSize), 
                            new Rectangle(x * 51, y * 51, 51, 51), 
                            GraphicsUnit.Pixel
                        );
                    }
                }

            }
            for (int j = 1; j <= 9; j++)
            {
                images[110 + j] = SetOpacity(images[j], 0.5f);
            }

            string fontName = "Microsoft Sans Serif";
            InstalledFontCollection installedFontCollection = new InstalledFontCollection();
            FontFamily[] fontFamilies = installedFontCollection.Families;
            string[] fontTargets = ("Noto Sans HK Medium,Noto Sans HK,Noto Sans HK Black,Noto Sans HK Light,Noto Sans HK Thin,Noto Sans TC,Noto Sans TC Black,Noto Sans TC Light,Noto Sans TC Medium,Noto Sans TC Regular,Noto Sans TC Thin,Noto Serif CJK TC Black,Noto Serif CJK TC Medium,Noto Sans CJK JP,Noto Sans CJK TC Black,Noto Sans CJK TC Bold,Noto Sans CJK TC Medium,Noto Sans CJK TC Regular,Noto Sans CJK DemiLight,Microsoft JhengHei").Split(',');


            foreach (string currfontName in fontTargets)
            {

                bool found = false;
                foreach (FontFamily fontFamily in fontFamilies)
                {
                    if (fontFamily.Name == currfontName)
                    {
                        fontName = currfontName;
                        found = true;
                        Console.WriteLine(fontName);
                        break;
                    }
                }
                if (found)
                {
                    break;
                }
            }

            //init buttons
            for (int i = 0; i < 11; i++)
            {
                Button b = new Button
                {
                    //Arial
                    //Microsoft Sans Serif
                    //Debug.WriteLine(b.Font.FontFamily);
                    Font = new Font(new FontFamily(fontName), 10),

                    Text = i.ToString()
                };

                Controls.Add(b);
                buttons.Add(b);
                b.BackgroundImageLayout = ImageLayout.Zoom;
                int j = i;
                if (i < 10)
                {
                    b.Click += new EventHandler((object Sender, EventArgs e) => PressKey(j));
                }
                else
                {
                    b.Click += new EventHandler((object Sender, EventArgs e) => CommandInput(Q9command.cancel));
                }
            }

            //----------------


            Rectangle screenSize = Screen.PrimaryScreen.Bounds;
            core = new Q9Core();

            //load ini------------------------




            var keys = "num1,num2,num3,num4,num5,num6,num7,num8,num9,num0,cancel".Split(',');
            var extraKeys = "relate,prev,shortcut,homo,openclose".Split(',');
            if (!ini.MakeExcite())
            {
                currBoxSize = 60;
                Left = screenSize.Width - currBoxSize * 3 + 20;
                Top = 100;

                ini.Write("Window", "Left", Left);
                ini.Write("Window", "Top", Top);
                ini.Write("Window", "BoxSize", currBoxSize);

                ini.Write("system", "sc_output", 0);
                ini.Write("system", "use_numpad", 1);

                ini.Write("AltKey", "num1", 'X');
                ini.Write("AltKey", "num2", 'C');
                ini.Write("AltKey", "num3", 'V');
                ini.Write("AltKey", "num4", 'S');
                ini.Write("AltKey", "num5", 'D');
                ini.Write("AltKey", "num6", 'F');
                ini.Write("AltKey", "num7", 'W');
                ini.Write("AltKey", "num8", 'E');
                ini.Write("AltKey", "num9", 'R');

                ini.Write("AltKey", "num0", 'Z');
                ini.Write("AltKey", "cancel", 'B');

                ini.Write("AltKey", "relate", 'G');
                ini.Write("AltKey", "prev", 'A');
                ini.Write("AltKey", "shortcut", 'A');
                ini.Write("AltKey", "homo", 'T');
                ini.Write("AltKey", "openclose", 'Q');

                //
                ini.Write("Key", "relate", 107); //107 is numpad +
                ini.Write("Key", "prev", 109); // -
                ini.Write("Key", "shortcut", 109); //-
                ini.Write("Key", "homo", 106); // *
                ini.Write("Key", "openclose", 111); // /

                //

                ini.Write("Key", "switch", 121);
                ini.Write("Key", "position", 120);
                ini.Write("Key", "size", 119);
            }
            else
            {
                currBoxSize = ini.ReadInt("Window", "BoxSize", Width);
                sc_output = Convert.ToBoolean(ini.ReadInt("system", "sc_output", 0));
                use_numpad = Convert.ToBoolean(ini.ReadInt("system", "use_numpad", 1));

            }

            //------------------------

            for (int k = 0; k < keys.Length; k++)
            {
                string keyName = keys[k];
                altKeys[keyName] = ini.ReadInt("AltKey", keyName, 0);
            }
            for (int k = 0; k < extraKeys.Length; k++)
            {
                string keyName = extraKeys[k];
                altKeys[keyName] = ini.ReadInt("AltKey", keyName, 0);
                Keys[keyName] = ini.ReadInt("Key", keyName, 0);
            }

            Keys["switch"] = ini.ReadInt("Key", "switch", 121);
            Keys["position"] = ini.ReadInt("Key", "position", 120);
            Keys["size"] = ini.ReadInt("Key", "size", 119);
            
            //MinimumSize= new Size(120, 160);
            FormBorderStyle = FormBorderStyle.Sizable;
            //AutoSizeMode = 0;
            Resize += Form1_Resize;
            //ClientSize = new Size(currBoxSize*3, currBoxSize * 4);
            SetNewWidth(currBoxSize * 3);

            Left = Math.Min(screenSize.Width - Width, ini.ReadInt("Window", "Left", Left));
            Top = Math.Min(screenSize.Height - Height, ini.ReadInt("Window", "Top", Top));

            Debug.WriteLine($"{Left},{screenSize.Width},{screenSize.Left},{screenSize.X}");


            FormClosing += Q9Form_FormClosing;

            Cancel();

            //tray---------------

            var components1 = new System.ComponentModel.Container();
            var contextMenu1 = new ContextMenu();



            //how2use
            MenuItem malert= new MenuItem();
            contextMenu1.MenuItems.AddRange(
                        new MenuItem[] { malert });
            malert.Text = "使用方法";
            malert.Click += new System.EventHandler((object Sender, EventArgs e) => MessageBox.Show(@"
《九万輸入法》使用方法:
需要開啟numLock

.           取消，特別強調0和.都不會因為要入關聯字或標點而改變用途
+           選擇關聯字 (在打下一個字前，都可隨時進入)
-           (選字時)上一頁
            (首頁) 快速選字 (碼表 id 1000)
            (首頁中按1~9後) 快速選字，共九種 (碼表 id 1001~1009)
*           同音字(打字後不出字，會進入同音字選字表)
/           「」等開關標點
0           首頁按0會進入普通標黜

scrollLock  開/關輸入法
F9          改變位置
F8          改變大小

可以自行修改`file/dataset.db`自行修改碼表，
例如調前常用字，加入emoji等，
推薦`DB Browser for SQLite`來修改



--------------------------------------

如果沒有num pad的電腦(如notebook)
可以選擇`不使用num pad`
那就會改為以n至/、j示至l、u至o來輸入

如想自行修改，可以開啓`tq9_settings.ini`
進入[AltKey]部份，自行修改 key code
google查`keycode online`隨便一個結果，都會有查key code的網頁
"));


            //sc
            var menuItemSC = new MenuItem
            {
                Index = 0,
                Text = "輸出簡體"
            };
            menuItemSC.Click += new System.EventHandler((object Sender, EventArgs e) => {
                sc_output = !sc_output;
                ((MenuItem)Sender).Checked = sc_output;
                ini.Write("system", "sc_output", sc_output ? 1 : 0);
            });
            menuItemSC.Checked = sc_output;
            contextMenu1.MenuItems.AddRange(new MenuItem[] { menuItemSC });


            //startup
            RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
            MenuItem startupItem = new MenuItem("開機自動開啟", new EventHandler((sender, e) => {
                if (key.GetValue(StartupValue) == null)
                {
                    key.SetValue(StartupValue, Application.ExecutablePath.ToString());
                    ((MenuItem)sender).Checked = true;
                }
                else
                {
                    key.DeleteValue(StartupValue);
                    ((MenuItem)sender).Checked = false;
                }
            }));
            if (key.GetValue(StartupValue) != null)
            {
                startupItem.Checked = true;
            }
            //contextMenu1.MenuItems.AddRange(new MenuItem[] { startupItem });




            //alt key
            MenuItem menuAltkey = new MenuItem("不使用num pad", new EventHandler((sender, e) =>
            {
                use_numpad = !use_numpad;
                ((MenuItem)sender).Checked = !use_numpad;
                ini.Write("system", "use_numpad", use_numpad ? 1 : 0);
            }))
            {
                Checked = !use_numpad
            };
            contextMenu1.MenuItems.AddRange(new MenuItem[] { menuAltkey });



            // exit
            var menuItem1 = new MenuItem();
            contextMenu1.MenuItems.AddRange(new MenuItem[] { menuItem1 });
            //menuItem1.Index = 1;
            menuItem1.Text = "離開";
            menuItem1.Click += new EventHandler((object Sender, EventArgs e) => Close());



            // Create the NotifyIcon.
            var notifyIcon1 = new NotifyIcon(components1)
            {
                Icon = new Icon("i.ico"),
                ContextMenu = contextMenu1,
                Text = "TQ9",
                Visible = true
            };
        }
        private Image SetOpacity(Image image, float opacity)
        {
            var colorMatrix = new ColorMatrix
            {
                Matrix33 = opacity
            };
            var imageAttributes = new ImageAttributes();
            imageAttributes.SetColorMatrix(
                colorMatrix,
                ColorMatrixFlag.Default,
                ColorAdjustType.Bitmap);
            var output = new Bitmap(image.Width, image.Height);
            using (var gfx = Graphics.FromImage(output))
            {
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.DrawImage(
                    image,
                    new Rectangle(0, 0, image.Width, image.Height),
                    0,
                    0,
                    image.Width,
                    image.Height,
                    GraphicsUnit.Pixel,
                    imageAttributes);
            }
            return output;
        }

        public static int Clamp(int val, int min, int max)
        {
            return Math.Max(Math.Min(val, max), min);
        }

        private readonly int[] lastSize = new int[] { 0, 0 };
        private void Form1_Resize(object sender, System.EventArgs e)
        {
            Control control = (Control)sender;

            int[] newSize = new int[] { control.ClientSize.Width, control.ClientSize.Height };
            //Debug.WriteLine("WH {0}x{1}", control.Size.Height, control.Size.Width);
            if (control.ClientSize.Width != lastSize[0] && lastSize[0] != 0)
            {                
                newSize[0] = Clamp(control.ClientSize.Width, 90, 450);
            }

            Debug.Write(Width);
            Debug.Write("_");
            Debug.WriteLine(control.ClientSize.Width);

            //Debug.WriteLine("{0}->{1}->{2}", control.Size.Width, control.ClientSize.Width,control.PreferredSize.Width);
            //Debug.WriteLine("{0}->{1}", lastSize[0], newSize[0]);
            SetNewWidth(newSize[0]);
        }
        private void SetNewWidth(int _width)
        {
            Control control = (Control)this;
            control.ClientSize = new Size(_width, (int)Math.Round(_width* 1.333));
            ResizeAllButton(control.ClientSize.Width / 3);
            lastSize[0] = control.ClientSize.Width;
            lastSize[1] = control.ClientSize.Height;
        }

        private int currBoxSize;

        public void ResizeAllButton(int boxSize)
        {
            currBoxSize = boxSize;
            for (int i = 0; i < 11; i++)
            {
                buttons[i].Width = buttons[i].Height = boxSize;

            }
            for (int i = 9, y = 0; i >= 3; i -= 3, y++)
            {
                for (int j = i, x = 2; j >= i - 2; j--, x--)
                {
                    buttons[j].Left = x * boxSize;
                    buttons[j].Top = y * boxSize;
                }
            }
            buttons[0].Top = buttons[10].Top = 3 * boxSize;
            buttons[0].Width = buttons[10].Left = 2 * boxSize;
            RenewFontsize();
        }

        public void SendOpenClose(string openclose)
        {
            // $"「」"
            SendKeys.Send(openclose);
            SendKeys.SendWait("{Left}");
        }

        private void SendText(string text)
        {
            if (sc_output)
            {
                SendKeys.Send(core.Tc2Sc(text));
            }
            else
            {
                SendKeys.Send(text);
            }
        }

        public bool HandleKey(int keyCode)
        {
            //scroll lock
            if (keyCode == Keys["switch"])
            {
                active = !active;
                if (!active)
                {
                    Hide();
                }
                else
                {
                    Show();
                    TopMost = true;

                    Rectangle screenSize = Screen.PrimaryScreen.Bounds;
                    Left = Clamp(Left, 0, screenSize.Width - Width);
                    Top = Clamp(Top, 0, screenSize.Height - Height);
                }
                return false;
            }

            //exit if special case
            if (!active)
            {
                return false;
            }

            if (keyCode == Keys["position"])
            {

                Show();
                TopMost = true;
                Rectangle screenSize = Screen.PrimaryScreen.Bounds;

                Left = screenSize.Width - Width - 30;

                Top = Top > (screenSize.Height - Height) / 2 ? 30 : screenSize.Height - Height - 65;
                return false;
            }

            if (keyCode == Keys["size"])
            {
                int newSize = currBoxSize;
                if (newSize == 40)
                {
                    newSize = 70;
                }else if (newSize == 70)
                {
                    newSize = 100;
                }else if (newSize == 100)
                {
                    newSize = 40;
                }
                else
                {
                    newSize = 70;
                }
                SetNewWidth(newSize * 3);
                return false;
            }

            if (!use_numpad)
            {
                for (int i = 0; i <= 9; i++)
                {
                    if (keyCode == altKeys[$"num{i}"])
                    {
                        PressKey(i);
                        return true;
                    }
                }

                if (keyCode == altKeys["cancel"])
                {
                    CommandInput(Q9command.cancel);
                }
                else if (keyCode == altKeys["relate"])
                {
                    CommandInput(Q9command.relate);
                }
                else if (keyCode == altKeys["homo"])
                {
                    CommandInput(Q9command.homo);
                }
                else if (keyCode == altKeys["openclose"])
                {
                    CommandInput(Q9command.openclose);
                }
                else if (keyCode == altKeys["shortcut"] && !selectMode)
                {
                    CommandInput(Q9command.shortcut);
                }
                else if (keyCode == altKeys["prev"] && selectMode)
                {
                    CommandInput(Q9command.prev);
                }
                else if(keyCode >= 65 && keyCode <=90)
                {
                    return true;
                }
                else
                {
                    return false;
                }
                return true;
            }

            //if num lock====
            if (keyCode >= 96 && keyCode <= 111)
            {
                if (keyCode >= 96 && keyCode <= 105)
                {
                    int inputInt = keyCode - 96;//0~9
                    PressKey(inputInt);
                    return true;
                }
                else
                {
                    if (keyCode == 110)
                    {
                        CommandInput(Q9command.cancel);
                    }
                    else if (keyCode == Keys["relate"])
                    {
                        CommandInput(Q9command.relate);
                    }
                    else if (keyCode == Keys["homo"])
                    {
                        CommandInput(Q9command.homo);
                    }
                    else if (keyCode == Keys["openclose"])
                    {
                        CommandInput(Q9command.openclose);
                    }
                    else if (keyCode == Keys["shortcut"] && !selectMode)
                    {
                        CommandInput(Q9command.shortcut);
                    }
                    else if (keyCode == Keys["prev"] && selectMode)
                    {
                        CommandInput(Q9command.prev);
                    }
                    else
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        //==================================================================================================

        public void SetButtonImg(int type)//0 1~9 10
        {
            for (int i = 1; i <= 9; i++)
            {
                int num = (type == 10 ? 11 : type) * 10 + i;
                //SetOpacity(images[num], type == 10 ? 0.5f : 1f);
                buttons[i].BackgroundImage = images[num];
                buttons[i].Text = "";
                buttons[i].BackColor = Color.White;

            }

            if (type == 0)
            {
                SetText(0, "標點");
            }
            else if (type <= 9)
            {
                SetText(0, "姓氏");
            }
            else if (type == 10)
            {
                SetText(0, "選字");
            }

            SetText(10, "取消");
        }

        public void SetZeroWords(string[] words)//
        {
            for (int i = 1; i <= 9; i++)
            {
                buttons[i].BackgroundImage = images[100 + i];
                buttons[i].BackColor = Color.White;
                if (i > words.Length || words[i - 1] == "*")
                {
                    buttons[i].Text = "";
                }
                else
                {
                    SetText(i, words[i-1], true);
                }
            }
            SetText(0, "標點");//
        }

        public void SetButtonsText(string[] words)
        {
            for (int i = 1; i <= 9; i++)
            {
                buttons[i].BackgroundImage = null;

                if (i >= words.Length || words[i] == "*")
                {
                    buttons[i].BackColor = Color.Gray;
                    buttons[i].Text = "";
                }
                else
                {
                    SetText(i, words[i]);
                }
            }
            //setText(0, buttons[0].Text);
            SetText(10, "取消");
        }

        private void SetText(int i, string s, bool relate = false)
        {
            buttons[i].BackColor = Color.White;
            buttons[i].Text = s;

            int fontsize;
            if (!relate)
            {
                float scale = i==10?0.5f:0.46f;
                fontsize = (int)((currBoxSize - 6) / Math.Max(1, s.Length) * scale * (i == 0 ? 1.8f : 1));
                //Debug.WriteLine(s, fontsize, currBoxSize);
                //Debug.WriteLine($"non relate:{s},{fontsize},{currBoxSize}");//"s, fontsize, currBoxSize);
                buttons[i].TextAlign = ContentAlignment.MiddleCenter;
                buttons[i].ForeColor = Color.Black;
            }
            else
            {
                float scale = i == 10 ? 0.5f : 0.28f;
                fontsize = (int)((currBoxSize - 6) / Math.Max(1, s.Length) * scale);
                if(i>0 && i < 10)
                {
                    buttons[i].TextAlign = ContentAlignment.TopLeft;
                    buttons[i].ForeColor = Color.Gray;
                }
                //Debug.WriteLine(s, fontsize, currBoxSize);
                //Debug.WriteLine($"relate:{s},{fontsize},{currBoxSize}");//"s, fontsize, currBoxSize);
            }
            buttons[i].Font = new Font(buttons[i].Font.FontFamily, fontsize);
        }

        private void RenewFontsize()
        {
            for (int i = 0; i <= 10; i++)
            {
                if (buttons[i].Text != "")
                {
                    SetText(i, buttons[i].Text, buttons[i].TextAlign == ContentAlignment.TopLeft);
                }
            }
        }

        //====================================================





        private string currCode = "";

        private bool homo = false;
        private bool openclose = false;
        private string lastWord = "";


        private void PressKey(int inputInt)//0~9
        {
            string inputStr = inputInt.ToString();

            if (selectMode)
            {
                if (inputInt == 0)
                {
                    CommandInput(Q9command.next);
                }
                else
                {
                    SelectWord(inputInt);
                }
            }
            else
            {
                currCode += inputStr;
                SetStatusPrefix(currCode);
                UpdateStatus();
                if (inputInt == 0)
                {
                    ProcessResult(core.KeyInput(Convert.ToInt32(currCode)));
                }
                else
                {
                    if (currCode.Length == 3)
                    {
                        ProcessResult(core.KeyInput(Convert.ToInt32(currCode)));
                    }
                    else if (currCode.Length == 1)
                    {
                        SetButtonImg(inputInt);
                    }
                    else
                    {
                        SetButtonImg(10);
                    }
                }
            }
            //SendKeys.Send("中");
        }


        private void CommandInput(Q9command command)
        {
            if (command == Q9command.cancel)
            {
                Cancel();
            }
            else if (command == Q9command.openclose)
            {
                homo = false;
                openclose = true;

                string opencloseStr = String.Join("",core.KeyInput(1));
                string[] opencloseArr = new string[(int)(opencloseStr.Length / 2.0)];
                for (int i = 0; i < opencloseStr.Length; i += 2)
                {
                    opencloseArr[i/2]=opencloseStr.Substring(i, 2);
                }
                SetStatusPrefix("「」");
                StartSelectWord(opencloseArr);
            }
            else if (command == Q9command.homo)
            {
                homo = !homo;
                RenewStatus();
            }
            else if (command == Q9command.shortcut && selectMode==false)
            {
                if (currCode.Length == 0)
                {
                    SetStatusPrefix("速選");
                    StartSelectWord(core.KeyInput(1000));
                }
                else if (currCode.Length == 1)
                {
                    SetStatusPrefix($"速選{Convert.ToInt32(currCode)}");
                    StartSelectWord(core.KeyInput(1000+Convert.ToInt32(currCode)));
                }
                
            }
            else if (command == Q9command.relate)
            {
                if (lastWord.Length==1)
                {
                    homo = false;
                    SetStatusPrefix($"[{lastWord}]關聯");
                    StartSelectWord(core.GetRelate(lastWord));
                }
            }
            else if (command == Q9command.prev && selectMode)
            {
                AddPage(-1);
            }
            else if (command == Q9command.next && selectMode)
            {
                AddPage(1);
            }


            //core.commandInput(command);
            //prev,next shortcut reset-position 0 related
        }

        private void Cancel(bool cleanRelate = true)
        {
            selectMode = false;
            homo = false;
            openclose = false;
            currCode = "";

            currPage = 0;
            selectWords = new string[0];

            SetStatusPrefix();
            UpdateStatus();

            if (cleanRelate)
            {
                SetButtonImg(0);
            }
        }

        public void ProcessResult(string[] words)
        {
            if (words==null || words.Length==0)
            {
                //* 
                Cancel();
                return;
            }
            StartSelectWord(words);
        }

        //===================================================================================

        private bool selectMode = false;
        private string[] selectWords = new string[0];
        private int currPage = 0;
        private int totalPage = 0;

        public void AddPage(int addNum)
        {
            if(currPage + addNum < 0)
            {
                ShowPage(totalPage-1);
            }
            else if (currPage + addNum >=totalPage)
            {
                ShowPage(0);
            }
            else
            {
                ShowPage(currPage + addNum);
            }
        }

        public void StartSelectWord(string[] words)
        {
            if(words==null || words.Length==0)return;

            selectWords= words;
            totalPage = (int)Math.Ceiling(words.Length / 9.0);
            selectMode = true;
            currCode = "";
            ShowPage(0);
            SetText(10, "取消");

            if (totalPage > 1)
            {
                SetText(0, "下頁");
            }
            else
            {
                SetText(0, "");
            }
        }

        public void ShowPage(int showPage)
        {
            currPage = showPage;
            string[] words = new string[10];
            for (int i = 1; i <= 9; i++)
            {
                int p = currPage * 9 + i - 1;
                if (p >= selectWords.Length || selectWords[p] == "*")
                {
                    words[i] = "";
                }
                else
                {
                    words[i] = selectWords[p];
                }
            }
            SetButtonsText(words);

            UpdateStatus(totalPage > 1 ? $"{currPage + 1}/{totalPage}頁" : "");
        }

        public void SelectWord(int inputInt)
        {
            int key = currPage * 9 + inputInt - 1;
            if(key>= selectWords.Length)
            {
                return;
            }
            string typeWord = selectWords[key];
            if (homo)
            {
                homo = false;
                SetStatusPrefix($"同音[{typeWord}]");
                StartSelectWord(core.GetHomo(typeWord));
                return;
            }
            else if (openclose)
            {
                openclose = false;
                SendOpenClose(typeWord);
                Cancel();
                return;
            }
            SendText(typeWord);
            string[] relates=new string[0];
            if (typeWord.Length == 1 )
            {
                lastWord = typeWord;
                relates = core.GetRelate(typeWord);
            }
            else
            {
                lastWord = "";
            }
            if (relates!=null && relates.Length > 0)
            {
                SetZeroWords(relates);
                Cancel(false);
            }
            else
            {
                Cancel();
            }

        }

        //===================================================================================
        private string statusPrefix;
        private string statusText;
        public void SetStatusPrefix(string _prefix = "")
        {
            statusPrefix = _prefix;
            RenewStatus();
        }
        public void UpdateStatus(string topText = "")
        {
            statusText = topText;
            RenewStatus();
        }
        public void RenewStatus()
        {
            Text = "九万 " + (homo ? "[同音] " : "") + statusPrefix + " " + statusText;
        }
    }
}