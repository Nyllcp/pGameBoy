using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using SFML.Audio;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace pGameBoy
{
    public partial class Main : Form
    {

        private RenderWindow _window;
        private Texture _texture;
        private Sprite _sprite;
        private DrawingSurface _drawingSurface;
        private Clock _clock;
        private Audio _audio;

        private Core _gameboy;
        private OpenFileDialog _ofd;

        private int lastTime;
        private int curentTime;
        private int frames;
        private int keyData;
        private int keyDataLast;
        private int fastforwardCounter;

        private bool frameLimit = true;
        private bool frameLimitToggle = false;
        private bool saveStateToggle = false;
        private bool run = false;

        const byte gbWidth = 160;
        const byte gbHeigth = 144;

        private byte[] _frame = new byte[160 * 144 * 4]; //4 Bytes per pixel

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        public Main()
        {
            InitializeComponent();
        }

        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Process.GetCurrentProcess().Id;
            int activeProcId;
            GetWindowThreadProcessId(activatedHandle, out activeProcId);

            return activeProcId == procId;
        }


       

        private void Main_Load(object sender, EventArgs e)
        {

            _ofd = new OpenFileDialog();
            //ofd.InitialDirectory = Environment.CurrentDirectory;
            _ofd.Filter = "Supported files (*.gb *.gbc *.zip)|*.gb;*.gbc;*.zip|All files (*.*)|*.*";
            _ofd.FilterIndex = 1;
            _ofd.RestoreDirectory = true;
            toolStripStatusFps.Text = "FPS: " + frames.ToString() + " Selected Save State : " + 0;

            _drawingSurface = new DrawingSurface();
            _drawingSurface.Size = new System.Drawing.Size(gbWidth * 5, gbHeigth * 5);
            _drawingSurface.ContextMenuStrip = rightClickMenu;
            this.ClientSize = new Size(_drawingSurface.Right, _drawingSurface.Bottom + menuStrip.Height + statusStrip.Height);
            Controls.Add(_drawingSurface);
            _drawingSurface.Location = new System.Drawing.Point(0, menuStrip.Height);

            InitSFML();
        }

        private void InitSFML()
        {
            _clock = new Clock();
            _audio = new Audio();
            _texture = new Texture(gbWidth, gbHeigth);
            _texture.Smooth = false;
            _sprite = new Sprite(_texture);
            _sprite.Scale = new Vector2f(5f, 5f);
            _window = new RenderWindow(_drawingSurface.Handle);
            _window.SetFramerateLimit(0);
            _texture.Update(_frame);
            _window.Clear();
            _window.Draw(_sprite);
            _window.Display();

        }

        private void SetScale(int multiplier)
        {
            multiplier = multiplier > 6 ? 6 : multiplier;

            _drawingSurface.Size = new System.Drawing.Size(gbWidth * multiplier, gbHeigth * multiplier);
            this.ClientSize = new Size(_drawingSurface.Right, _drawingSurface.Bottom  + menuStrip.Height);
            _drawingSurface.Location = new System.Drawing.Point(0, menuStrip.Height);
            //_sprite.Scale = new Vector2f((float)multiplier, (float)multiplier);
            _texture.Update(_frame);
            _window.Clear();
            _window.Draw(_sprite);
            _window.Display();

        }



        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            run = false;
            Application.Exit();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_gameboy != null) _gameboy.WriteSave();
            if (_ofd.ShowDialog() == DialogResult.OK)
            {
                run = false;
                _gameboy = new Core();
                if(_gameboy.LoadRom(_ofd.FileName))
                {
                    this.Text = "pGameBoy - " +_gameboy.CurrentRomName;
                    _clock.Restart().AsMilliseconds();
                    RunEmulation();
                }
                else
                {
                    this.Text = "pGameBoy - " + "Invalid ROM / File";
                }
            }
        }

        private void RunEmulation()
        {
            run = true;
            int lastCycles = 0;
            lastTime = _clock.ElapsedTime.AsMilliseconds();
            int _savetimer = 0;
            while (run)
            {
                _drawingSurface.Select();
                if (ApplicationIsActivated())
                {
                    Input();
                    while (!_gameboy.Frameready)
                    {
                        _gameboy.MachineCycle();
                    }
                }

                UpdateFrameRGB(_gameboy.FrambufferRGB);
                _texture.Update(_frame);
                _window.Clear();
                _window.Draw(_sprite);
                
                if(frameLimit)
                {
                    _audio.AddSample(_gameboy.GetSamples, _gameboy.NumberOfSamples, true);
                    while (_audio.GetBufferedBytes() > (((44100) / 60 * 2) * 4))
                    {
                        System.Threading.Thread.Sleep(1);
                        //Max 4 frames of audio lag, cant go lower probably cause of thread sleep beeing useless.
                    }

                }   
                //else
                //{
                //    if(fastforwardCounter % 3 == 0) { _audio.AddSample(_gameboy.GetSamples, _gameboy.NumberOfSamples, true); }
                //    while (_audio.GetBufferedBytes() > (((44100) / 60 * 2) * 4))
                //    {
                //        System.Threading.Thread.Sleep(1);
                //        //Max 4 frames of audio lag, cant go lower probably cause of thread sleep beeing useless.
                //    }
                //}            
                System.Windows.Forms.Application.DoEvents(); // handle form events
                _window.DispatchEvents(); // handle SFML events - NOTE this is still required when SFML is hosted in another window
                fastforwardCounter++;
                frames++;
                curentTime = _clock.ElapsedTime.AsMilliseconds();
                if (curentTime - lastTime > 1000)
                {
                    _savetimer++;
                    int cyclesperframe = (_gameboy.CpuCycles - lastCycles) / frames;
                    lastCycles = _gameboy.CpuCycles;
                    //toolStripStatusFps.Text = "FPS: " + frames.ToString() + " Cpu Cycles Per Frame : " + cyclesperframe.ToString() + " Bytes in audio buffer:" + _audio.GetBufferedBytes();
                    toolStripStatusFps.Text = "FPS: " + frames.ToString() + " Selected Save State : " + (_gameboy.SelectedSavestate + 1);
                    frames = 0;
                    if(_savetimer > 60) //Write savefile to disk every 1 minute(s).
                    {
                        _savetimer = 0;
                        _gameboy.WriteSave(); 
                    }
                    lastTime = _clock.ElapsedTime.AsMilliseconds();
                }
                _window.Display();

            }

        }


        private void UpdateFrameRGB(uint[] gbFrame)
        {
            for (int i = 0; i < gbFrame.Length; i++)
            {
                _frame[i * 4] = (byte)(gbFrame[i] & 0xFF);
                _frame[i * 4 + 1] = (byte)((gbFrame[i] >> 8) & 0xFF);
                _frame[i * 4 + 2] = (byte)((gbFrame[i] >> 16) & 0xFF);
                _frame[i * 4 + 3] = (byte)((gbFrame[i] >> 24) & 0xFF);
            }
        }

        private void Input()
        {
            keyDataLast = keyData;
            keyData = 0xFF;
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Down))
            {
                keyData &= ~(1 << 7);
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Up))
            {
                keyData &= ~(1 << 6);
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Left))
            {
                keyData &= ~(1 << 5);
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Right))
            {
                keyData &= ~(1 << 4);
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.S) || SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Return))
            {
                keyData &= ~(1 << 3);
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.A) || SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.RShift))
            {
                keyData &= ~(1 << 2);
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Z))
            {
                keyData &= ~(1 << 1);
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.X))
            {
                keyData &= ~(1 << 0);
            }


            if (keyData != keyDataLast)
            {
                _gameboy.UpdatePad((byte)keyData);
            }
            
            if(SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Q) || SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.C) && !frameLimitToggle)
            {
                frameLimit = !frameLimit;
            }
            frameLimitToggle = SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.Q) | SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.C);

            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.E) && !saveStateToggle)
            {
                _gameboy.SaveState = true;
            }
            if (SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.R) && !saveStateToggle)
            {
                _gameboy.LoadState = true;
            }
            saveStateToggle = SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.R) | SFML.Window.Keyboard.IsKeyPressed(Keyboard.Key.E);

        }

        private void toggleFramelimitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frameLimit = !frameLimit;
        }

        private void smoothTextureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _texture.Smooth = !_texture.Smooth;
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_gameboy != null)
                _gameboy.Reset();
        }

        private void SetScaleClick(object sender, EventArgs e)
        {
            ToolStripMenuItem value = sender as ToolStripMenuItem;
            SetScale(int.Parse(value.Tag.ToString()));
        }
        private void SetSaveStateClick(object sender, EventArgs e)
        {
            ToolStripMenuItem value = sender as ToolStripMenuItem;
            if (_gameboy != null)
                _gameboy.SelectedSavestate = int.Parse(value.Text.ToString()) - 1;
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            run = false;
            Application.Exit();
        }

        private void loadStateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(_gameboy != null)
                _gameboy.LoadState = true;
        }

        private void saveStateToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (_gameboy != null)
                _gameboy.SaveState = true;     
        }

        public class DrawingSurface : System.Windows.Forms.Control
        {
            protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
            {
                // don't call base.OnPaint(e) to prevent forground painting
                // base.OnPaint(e);
            }
            protected override void OnPaintBackground(System.Windows.Forms.PaintEventArgs pevent)
            {
                // don't call base.OnPaintBackground(e) to prevent background painting
                //base.OnPaintBackground(pevent);
            }
        }
    }
}
