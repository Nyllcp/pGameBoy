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

        private bool frameLimit = true;
        private bool frameLimitToggle = false;
        private bool run = false;

        const byte gbWidth = 160;
        const byte gbHeigth = 144;

        private byte[] _frame = new byte[160 * 144 * 4]; //4 Bytes per pixel

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            InitSFML();
            _ofd = new OpenFileDialog();
            //ofd.InitialDirectory = Environment.CurrentDirectory;
            _ofd.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            _ofd.FilterIndex = 2;
            _ofd.RestoreDirectory = true;
            
        }

        private void InitSFML()
        {
            _clock = new Clock();
            _audio = new Audio();

            _drawingSurface = new DrawingSurface();
            _drawingSurface.Size = new System.Drawing.Size(gbWidth * 5, gbHeigth * 5);

            this.ClientSize = new Size(_drawingSurface.Right, _drawingSurface.Bottom + statusStrip.Size.Height + menuStrip.Height);
            Controls.Add(_drawingSurface);
            _drawingSurface.Location = new System.Drawing.Point(0, menuStrip.Height);

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
                Input();
                while (!_gameboy.Frameready)
                {
                    _gameboy.MachineCycle();
                }
                UpdateFrame(_gameboy.Frambuffer, Palette.BGB);
                _texture.Update(_frame);
                _window.Clear();
                _window.Draw(_sprite);
                _audio.AddSample(_gameboy.GetSamples, _gameboy.NumberOfSamples, true);
                if(frameLimit)
                {
                    while (_audio.GetBufferedBytes() > (((44100) / 60 * 2) * 4))
                    {
                        System.Threading.Thread.Sleep(1);
                        //Max 4 frames of audio lag, cant go lower probably cause of thread sleep beeing useless.
                    }

                }               
                System.Windows.Forms.Application.DoEvents(); // handle form events
                _window.DispatchEvents(); // handle SFML events - NOTE this is still required when SFML is hosted in another window
                
                frames++;
                curentTime = _clock.ElapsedTime.AsMilliseconds();
                if (curentTime - lastTime > 1000)
                {
                    _savetimer++;
                    int cyclesperframe = (_gameboy.CpuCycles - lastCycles) / frames;
                    lastCycles = _gameboy.CpuCycles;
                    toolStripStatusFps.Text = "FPS: " + frames.ToString() + " Cpu Cycles Per Frame : " + cyclesperframe.ToString() + " Bytes in audio buffer:" + _audio.GetBufferedBytes();
                    frames = 0;
                    if(_savetimer > 60 * 3) //Write savefile to disk every 3 minutes.
                    {
                        _savetimer = 0;
                        _gameboy.WriteSave(); 
                    }
                    lastTime = _clock.ElapsedTime.AsMilliseconds();
                }
                _window.Display();

            }

        }

        private void UpdateFrame(byte[] gbFrame , uint[] pallete)
        {
            for(int i = 0; i < gbFrame.Length; i++)
            {
                _frame[i * 4] = (byte)(pallete[gbFrame[i]] & 0xFF);
                _frame[i * 4 + 1] = (byte)((pallete[gbFrame[i]] >> 8) & 0xFF);
                _frame[i * 4 + 2] = (byte)((pallete[gbFrame[i]] >> 16) & 0xFF);
                _frame[i * 4 + 3] = (byte)((pallete[gbFrame[i]] >> 24) & 0xFF);
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
            _gameboy.Reset();
        }

        private void xToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
