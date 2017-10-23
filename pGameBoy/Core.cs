using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class Core
    {
        

        private byte[] RAM = new byte[0x2000];
        private byte[] VRAM = new byte[0x2000];
        private byte[] ZeroPage = new byte[0x80];
        

        private byte p1; //Joypad
        private byte div; // divider register
        private ushort tima; //Timer counter
        private byte tma; //Timer modulo
        private byte tac; //Timer Control
        
        
        
        
        private byte dma; //Dma adress

        
        private byte serialdata;
        private byte serialreg;
        private ushort div_clockcount;
        private byte[] ioregisters = new byte[0x100];
        private byte keydata1 = 0xF;
        private byte keydata2 = 0xF;
        

        private byte ie; //Interrupt enable
        private byte iflag; //Interruptflag

        private int timer_clockcount;
        private int lastcycles;

        //Interrupt constants
        const byte vblank_const = 0x01, LCDC_const = 0x02, Timeroverflow_const = 0x04, Serial_const = 0x08, negativeedge_const = 0x10;



        private Cart _cart;
        private Cpu _cpu;
        private Lcd _lcd;
        private Apu _apu;

        public bool Frameready { get { return _lcd.Frameready; } set { _lcd.Frameready = value; } }
        public byte[] Frambuffer { get { _lcd.Frameready = false; return _lcd.Framebuffer; } }
        public string CurrentRomName { get { return _cart.CurrentRomName; } }
        public int CpuCycles { get { return _cpu.Cycles; } }
        public byte[] GetSamples { get { return _apu.Samples; } }
        public int NumberOfSamples { get { int value = _apu.NumberOfSamples; _apu.NumberOfSamples = 0; return value; } }

        public Core()
        {
            _cart = new Cart();
            _cpu = new Cpu(this);
            _lcd = new Lcd(this);
            _apu = new Apu();
        }

        //       Interrupt Enable Register
        //--------------------------- FFFF
        //Internal RAM
        //--------------------------- FF80
        //Empty but unusable for I/O
        //--------------------------- FF4C
        //I/O ports
        //--------------------------- FF00
        //Empty but unusable for I/O
        //--------------------------- FEA0
        //Sprite Attrib Memory(OAM)
        //--------------------------- FE00
        //Echo of 8kB Internal RAM
        //--------------------------- E000
        //8kB Internal RAM
        //--------------------------- C000
        //8kB switchable RAM bank
        //--------------------------- A000
        //8kB Video RAM
        //--------------------------- 8000 --
        //16kB switchable ROM bank |
        //--------------------------- 4000 |= 32kB Cartrigbe
        //16kB ROM bank #0 |
        //--------------------------- 0000 --

        public bool LoadRom(string name)
        {   
            Reset();
            return _cart.LoadRom(name);
        }

        public void WriteSave()
        {
            _cart.WriteSaveFile();
        }
        public byte ReadMem(ushort address)
        {

            if (address < 0x8000)
            {
                return _cart.ReadCart(address);
            }
            else if (address < 0xA000)
            {
                return VRAM[address & 0x1FFF];
            }
            else if (address < 0xC000)
            {
                return _cart.ReadCartRam(address);
            }
            else if (address < 0xFE00)
            {
                return RAM[address & 0x1FFF];
            }
            else if (address < 0xFF00)
            {
                return _lcd.OAM[address & 0xFF];
            }
            else if (address >= 0xFF00 && address < 0xFF80 || address == 0xFFFF)
            {
                return ReadIO(address);
            }
            else if (address >= 0xff80 && address < 0xFFFF)
            {
                return ZeroPage[address & 0x7F];
            }
            return 0;

        }
        public void WriteMem(ushort address, byte data)
        {
            if (address < 0x8000)
            {
                _cart.WriteCart(address, data);
            }
            else if (address < 0xA000)
            {
                VRAM[address & 0x1FFF] = data;
            }
            else if (address < 0xC000)
            {
                _cart.WriteCartRam(address, data);
            }
            else if (address < 0xFE00)
            {
                RAM[address & 0x1FFF] = data;
            }
            else if (address < 0xFF00)
            {
                _lcd.OAM[address & 0xFF] = data;
            }
            else if (address >= 0xFF00 && address < 0xFF80 || address == 0xFFFF)
            {
                WriteIO(address, data);
            }
            else if (address >= 0xff80 && address < 0xFFFF)
            {
                ZeroPage[address & 0x7F] = data;
            }
        }
        public void MachineCycle()
        {
            lastcycles = _cpu.Cycles;
            HandleInterrupt();
            _cpu.Cycle();

            for (int i = 0; i < _cpu.Cycles - lastcycles; i++)
            {
                _lcd.LcdTick();  
                Timer();
                _apu.ApuTick();
            }
        }
        public byte ReadIO(ushort address)
        {
            if ((address & 0xFF) >= 0x10 && (address & 0xFF) <= 0x3F)
            {
                return _apu.ReadSoundRegister(address);
            }
            switch (address & 0xff)
            {
                case 0x00: return p1;
                case 0x01: return serialdata; //Serial transder data cba
                case 0x02: return serialreg; //Serial transfer bs cba
                case 0x04: return div;
                case 0x05: return (byte)tima;
                case 0x06: return tma;
                case 0x07: return tac;
                case 0x0F: return iflag;
                //soundshit goes here
                case 0x40: return _lcd.ReadLcdRegister(address);
                case 0x41: return _lcd.ReadLcdRegister(address);
                case 0x42: return _lcd.ReadLcdRegister(address);
                case 0x43: return _lcd.ReadLcdRegister(address);
                case 0x44: return _lcd.ReadLcdRegister(address);
                case 0x45: return _lcd.ReadLcdRegister(address);
                case 0x46: return 0;
                case 0x47: return _lcd.ReadLcdRegister(address);
                case 0x48: return _lcd.ReadLcdRegister(address);
                case 0x49: return _lcd.ReadLcdRegister(address);
                case 0x4A: return _lcd.ReadLcdRegister(address);
                case 0x4B: return _lcd.ReadLcdRegister(address);
                case 0xFF: return ie;

                default: return ioregisters[address & 0xFF];


            }

        }
        private void WriteIO(ushort address, byte data)
        {
            ioregisters[address & 0xFF] = data;
            switch (address & 0xff)
            {
                case 0x00: JoypadWrite(data); break;
                case 0x01: serialdata = data; ; break; //Serial transfer data, printing to console as char
                case 0x02:
                    serialreg = data;
                    //Console.Write(Convert.ToChar(serialdata));
                    break;//Serial transder data cba
                case 0x04: div = 0; break;
                case 0x05: tima = data; break;
                case 0x06: tma = data; break;
                case 0x07: tac = data; break;
                case 0x0F:
                    byte temp = (byte)(0xE0 | (data & 0x1f));
                    iflag = temp;
                    break;
                //Soundshit goes here
                case 0x40: _lcd.WriteLcdRegister(address, data); break;
                case 0x41: _lcd.WriteLcdRegister(address, data); break;
                case 0x42: _lcd.WriteLcdRegister(address, data); break;
                case 0x43: _lcd.WriteLcdRegister(address, data); break;
                case 0x44: break;
                case 0x45: _lcd.WriteLcdRegister(address, data); break;
                case 0x46:
                    dma = data;
                    for (int i = 0; i < 0xA0; i++)
                    {
                        WriteMem((ushort)(0xFE00 + i), ReadMem((ushort)((data << 8) + i)));
                    }
                    break;
                case 0x47: _lcd.WriteLcdRegister(address, data); break;
                case 0x48: _lcd.WriteLcdRegister(address, data); break;
                case 0x49: _lcd.WriteLcdRegister(address, data); break;
                case 0x4A: _lcd.WriteLcdRegister(address, data); break;
                case 0x4B: _lcd.WriteLcdRegister(address, data); break;
                case 0xFF: ie = data; break;

                default: break;
            }
            //Sound registers are mapped to $FF10-$FF3F in memory. 
            if ((address & 0xFF) >= 0x10 && (address & 0xFF) <= 0x3F)
            {
                _apu.WriteSoundRegister(address, data);
            }


        }
        public void SetIflag(byte value)
        {
            iflag |= value;
        }
        public void JoypadWrite(byte data)
        {
            byte oldp1 = p1;
            int keydata = 0xF;

            int newp1 = (data & 0xF0) | (oldp1 & ~0xF0);
            if ((newp1 != oldp1) && ((newp1 & 0x0F) != 0x0F))
            {
                iflag |= 0x10;
            }
            if (((newp1 >> 4) & 1) == 0)
            {
                keydata = keydata1;
            }
            else if (((newp1 >> 5) & 1) == 0)
            {
                keydata = keydata2;
            }

            newp1 = ((newp1 & 0xF0) | (keydata & 0xF));
            p1 = (byte)newp1;
        }
        public void UpdatePad(byte data)
        {
            keydata1 = (byte)(data >> 4);
            keydata2 = (byte)(data & 0xf);
            if (keydata1 != 0xf || keydata2 != 0xf)
            {
                iflag |= 0x10;
            }
        }
        public void Reset()
        {
            _cpu.ResetCpu();
            _lcd.ResetLcd();
            _apu.ResetApu();
            tima = 0;
            tma = 0;
            tac = 0;
            ie = 0;
            for(int i = 0; i < RAM.Length; i++)
            {
                RAM[i] = 0;
            }
            for (int i = 0; i < VRAM.Length; i++)
            {
                VRAM[i] = 0;
            }
            for (int i = 0; i < ZeroPage.Length; i++)
            {
                ZeroPage[i] = 0;
            }
        }
        private void Timer()
        {

            div_clockcount++;
            if ((tac & 0x04) != 0)
            {
                timer_clockcount++;
                switch (tac & 0x03)
                {
                    case 0: if (timer_clockcount >= 1024) { tima++; timer_clockcount = 0; } break;
                    case 1: if (timer_clockcount >= 16) { tima++; timer_clockcount = 0; } break;
                    case 2: if (timer_clockcount >= 64) { tima++; timer_clockcount = 0; } break;
                    case 3: if (timer_clockcount >= 256) { tima++; timer_clockcount = 0; } break;

                }

            }
            if (tima > 255)
            {
                timer_clockcount = 0;
                tima = tma;
                iflag |= Timeroverflow_const;
            }
            if (div_clockcount > 255)
            {
                div++;
                div_clockcount = 0;
            }
        }
        private void HandleInterrupt()
        {
            if ((iflag & ie) != 0)
            {
                _cpu.Halt = false;
            }
            if (_cpu.InterruptsEnabled)
            {
                if ((iflag & vblank_const) != 0 && (ie & vblank_const) != 0)
                {
                    int mask = ~vblank_const;
                    iflag &= (byte)(mask);
                    _cpu.Interrupt(0x040);
                }
                else if ((iflag & LCDC_const) != 0 && (ie & LCDC_const) != 0)
                {
                    int mask = ~LCDC_const;
                    iflag &= (byte)(mask);
                    _cpu.Interrupt(0x048);
                }
                else if ((iflag & Timeroverflow_const) != 0 && (ie & Timeroverflow_const) != 0)
                {
                    int mask = ~Timeroverflow_const;
                    iflag &= (byte)(mask);
                    _cpu.Interrupt(0x050);
                }
                else if ((iflag & Serial_const) != 0 && (ie & Serial_const) != 0)
                {
                    int mask = ~Serial_const;
                    iflag &= (byte)(mask);
                    _cpu.Interrupt(0x058);
                }
                else if ((iflag & negativeedge_const) != 0 && (ie & negativeedge_const) != 0)
                {
                    int mask = ~negativeedge_const;
                    iflag &= (byte)(mask);
                    _cpu.Interrupt(0x060);
                }
            }
        }
    }
}
