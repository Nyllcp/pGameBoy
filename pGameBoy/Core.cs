using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class Core
    {

        private Cart _cart;
        private Cpu _cpu;
        private PPU _ppu;
        private Apu _apu;
        private Savestate _saveState = new Savestate();

        //Interrupt constants
        const byte vblank_const = 0x01, LCDC_const = 0x02, Timeroverflow_const = 0x04, Serial_const = 0x08, negativeedge_const = 0x10;

        //SaveState
        private bool saveState = false;
        private bool loadState = false;
        private int selectedSavestate = 0;

        //RAM
        private byte[,] WRAM = new byte[8,0x1000];
        private byte[] ZeroPage = new byte[0x80];
        private byte wramBankNo = 1;
        private byte p1; //Joypad
        private ushort div; // divider register
        private byte tima; //Timer counter
        private byte tma; //Timer modulo
        private byte tac; //Timer Control
        private byte dma; //Dma adress

        //CGB Variables
        private bool gbcMode = false;
        private bool doubleSpeed = false;
        private bool prepareSpeedSwitch = false;
        private byte serialdata;
        private byte serialreg;
        private byte[] ioregisters = new byte[0x100];
        private byte keydata1 = 0xF;
        private byte keydata2 = 0xF;
        private byte ie = 0xE0; //Interrupt enable
        private byte iflag = 0xE0; //Interruptflag
        private int lastcycles;


        public bool Frameready { get { return _ppu.Frameready; } set { _ppu.Frameready = value; } }
        public uint[] FrambufferRGB { get { _ppu.Frameready = false; return _ppu.FramebufferRGB; } }
        public string CurrentRomName { get { return _cart.CurrentRomName; } }
        public int CpuCycles { get { return _cpu.Cycles; } }
        public byte[] GetSamples { get { return _apu.Samples; } }
        public int NumberOfSamples { get { int value = _apu.NumberOfSamples; _apu.NumberOfSamples = 0; return value; } }
        public bool GbcMode { get { return gbcMode; } set { gbcMode = value; } }
        public bool SaveState { get { return saveState; } set { saveState = value; } }
        public bool LoadState { get { return loadState; } set { loadState = value; } }
        public int SelectedSavestate { get { return selectedSavestate; } set { selectedSavestate = value > 9 ? 9 : value; } }

        public Core()
        {
            _cart = new Cart();
            _cpu = new Cpu(this);
            _ppu = new PPU(this);
            _apu = new Apu();
        }


        public void MachineCycle()
        {
            
            lastcycles = _cpu.Cycles;
            if (prepareSpeedSwitch)
            {
                if (_cpu.Stop)
                {
                    doubleSpeed = !doubleSpeed;
                    prepareSpeedSwitch = false;
                    _cpu.Stop = false;
                }
            }
            if (doubleSpeed)
            {
                HandleInterrupt();
                _cpu.Cycle();
                HandleInterrupt();
                _cpu.Cycle();
                for (int i = 0; i < _cpu.Cycles - lastcycles; i++)
                {
                    Timer();
                }
                for (int i = 0; i < (_cpu.Cycles - lastcycles) / 2; i++)
                {
                    _ppu.LcdTick();
                    _apu.ApuTick();
                }
            }
            else
            {
                HandleInterrupt();
                _cpu.Cycle();

                for (int i = 0; i < _cpu.Cycles - lastcycles; i++)
                {
                    _ppu.LcdTick();
                    Timer();
                    _apu.ApuTick();
                }
            }
            if(saveState)
            {
                WriteSaveState(ref _saveState);
                _cart.WriteStateFile(_saveState, selectedSavestate);
                saveState = false;
            }
            if(loadState)
            {
                _saveState = _cart.LoadStateFile(selectedSavestate);
                LoadSaveState(_saveState);
                loadState = false;
            }


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
            
            if (!_cart.LoadRom(name)) return false;
            gbcMode = _cart.GbcRom;
            Reset();
            return true;
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
                return _ppu.ReadVram(address);
            }
            else if (address < 0xC000)
            {
                return _cart.ReadCartRam(address);
            }
            else if (address < 0xD000)
            {
                return WRAM[0,address & 0xFFF];
            }
            else if (address < 0xE000)
            {
                return WRAM[wramBankNo, address & 0xFFF];
            }
            else if (address < 0xFE00)
            {
                //echo of ram, return 0 for now
                return 0;
            }
            else if (address < 0xFF00)
            {
                return _ppu.OAM[address & 0xFF];
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
                _ppu.WriteVram(address, data);
            }
            else if (address < 0xC000)
            {
                _cart.WriteCartRam(address, data);
            }
            else if (address < 0xD000)
            {
                WRAM[0, address & 0xFFF] = data;
            }
            else if (address < 0xE000)
            {
                WRAM[wramBankNo, address & 0xFFF] = data;
            }
            else if (address < 0xFE00)
            {
                //echo of ram do nothich for now
                return;
            }
            else if (address < 0xFF00)
            {
                _ppu.OAM[address & 0xFF] = data;
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
                case 0x04: return (byte)(div >>8);
                case 0x05: return tima;
                case 0x06: return tma;
                case 0x07: return tac;
                case 0x0F: return iflag;
                //soundshit goes here
                case 0x40: return _ppu.ReadLcdRegister(address);
                case 0x41: return _ppu.ReadLcdRegister(address);
                case 0x42: return _ppu.ReadLcdRegister(address);
                case 0x43: return _ppu.ReadLcdRegister(address);
                case 0x44: return _ppu.ReadLcdRegister(address);
                case 0x45: return _ppu.ReadLcdRegister(address);
                case 0x46: return dma;
                case 0x47: return _ppu.ReadLcdRegister(address);
                case 0x48: return _ppu.ReadLcdRegister(address);
                case 0x49: return _ppu.ReadLcdRegister(address);
                case 0x4A: return _ppu.ReadLcdRegister(address);
                case 0x4B: return _ppu.ReadLcdRegister(address);
                case 0x4D:
                        if(gbcMode)
                        {
                            byte value = (byte)(prepareSpeedSwitch ? 1 : 0);
                            value |= (byte)(doubleSpeed ? (1 << 7) : 0);
                            return value;
                        }
                        return ioregisters[address & 0xFF];
                case 0x4F:
                    if (gbcMode) return _ppu.ReadLcdRegister(address);
                    return ioregisters[address & 0xFF];
                case 0x51: return _ppu.ReadLcdRegister(address);
                case 0x52: return _ppu.ReadLcdRegister(address);
                case 0x53: return _ppu.ReadLcdRegister(address);
                case 0x54: return _ppu.ReadLcdRegister(address);
                case 0x55: return _ppu.ReadLcdRegister(address);
                case 0x68: return _ppu.ReadLcdRegister(address);
                case 0x69: return _ppu.ReadLcdRegister(address);
                case 0x6A: return _ppu.ReadLcdRegister(address);
                case 0x6B: return _ppu.ReadLcdRegister(address); 
                case 0x70:
                        if (gbcMode)
                        {
                            return wramBankNo;
                        }
                        return ioregisters[address & 0xFF];
                case 0xFF: return ie;

                default: return ioregisters[address & 0xFF];


            }

        }
        private void WriteIO(ushort address, byte data)
        {
            ioregisters[address & 0xFF] = data;
            //Sound registers are mapped to $FF10-$FF3F in memory. 
            if ((address & 0xFF) >= 0x10 && (address & 0xFF) <= 0x3F)
            {
                _apu.WriteSoundRegister(address, data);
            }

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
                case 0x0F: iflag = (byte)(0xE0 | (data & 0x1f)); break;
                     
                //Soundshit goes here

                case 0x40: _ppu.WriteLcdRegister(address, data); break;
                case 0x41: _ppu.WriteLcdRegister(address, data); break;
                case 0x42: _ppu.WriteLcdRegister(address, data); break;
                case 0x43: _ppu.WriteLcdRegister(address, data); break;
                case 0x44: break;
                case 0x45: _ppu.WriteLcdRegister(address, data); break;
                case 0x46:
                    dma = data;
                    for (int i = 0; i < 0xA0; i++)
                    {
                        WriteMem((ushort)(0xFE00 + i), ReadMem((ushort)((dma << 8) + i)));
                    }
                    break;
                case 0x47: _ppu.WriteLcdRegister(address, data); break;
                case 0x48: _ppu.WriteLcdRegister(address, data); break;
                case 0x49: _ppu.WriteLcdRegister(address, data); break;
                case 0x4A: _ppu.WriteLcdRegister(address, data); break;
                case 0x4B: _ppu.WriteLcdRegister(address, data); break;
                case 0x4D:
                        if(gbcMode)
                        {
                            prepareSpeedSwitch = (data & 1) != 0;
                            
                        }
                        break;
                case 0x4F:
                    if (gbcMode)
                    {
                        _ppu.WriteLcdRegister(address, data); 
                    }
                    break;
                case 0x51: _ppu.WriteLcdRegister(address, data); break;
                case 0x52: _ppu.WriteLcdRegister(address, data); break;
                case 0x53: _ppu.WriteLcdRegister(address, data); break;
                case 0x54: _ppu.WriteLcdRegister(address, data); break;
                case 0x55: _ppu.WriteLcdRegister(address, data); break;
                case 0x68: _ppu.WriteLcdRegister(address, data); break;
                case 0x69: _ppu.WriteLcdRegister(address, data); break;
                case 0x6A: _ppu.WriteLcdRegister(address, data); break;
                case 0x6B: _ppu.WriteLcdRegister(address, data); break;
                case 0x70:
                        if (gbcMode)
                        {
                            wramBankNo = (byte)(data & 0x7);
                            wramBankNo = (byte)(wramBankNo == 0 ? 1 : wramBankNo);
                        }
                        break;
                case 0xFF: ie = (byte)(0xE0 | (data & 0x1f)); break;


                default: break;
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
            _ppu.ResetLcd();
            _apu.ResetApu();
            tima = 0;
            tma = 0;
            tac = 0;
            ie = 0xE0;
            iflag = 0xE0;
            doubleSpeed = false;
            WRAM = new byte[8, 0x1000];     
            ZeroPage = new byte[0x80];
    }
        private void Timer()
        {

            div++;
            if ((tac & 0x04) != 0)
            {   
                switch (tac & 0x03)
                {
                    case 0: if ((div & 1023) == 0)
                        {
                            if(++tima == 0)
                            {
                                tima = tma;
                                iflag |= Timeroverflow_const;
                            }
                        }
                        break;
                    case 1: if ((div & 15) == 0)
                        {
                            if (++tima == 0)
                            {
                                tima = tma;
                                iflag |= Timeroverflow_const;
                            }
                        }
                        break;
                    case 2: if ((div & 63) == 0)
                        {
                            if (++tima == 0)
                            {
                                tima = tma;
                                iflag |= Timeroverflow_const;
                            }
                        }
                        break;
                    case 3: if ((div & 255) == 0)
                        {
                            if (++tima == 0)
                            {
                                tima = tma;
                                iflag |= Timeroverflow_const;
                            }
                        }
                        break;

                }

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
        public Savestate WriteSaveState(ref Savestate state)
        {
            _cpu.WriteSaveState(ref state);
            _ppu.WriteSaveState(ref state);
            _apu.WriteSaveState(ref state);
            Array.Copy(WRAM, state.WRAM, WRAM.Length);
            Array.Copy(ZeroPage, state.ZeroPage, ZeroPage.Length);
            Array.Copy(ioregisters, state.ioregisters, ioregisters.Length);
            state.wramBankNo = wramBankNo;
            state.p1 = p1;
            state.div = div;
            state.tima = tima;
            state.tma = tma;
            state.tac = tac;
            state.dma = dma;
            state.gbcMode = gbcMode;
            state.doubleSpeed = doubleSpeed;
            state.prepareSpeedSwitch = prepareSpeedSwitch;
            state.serialdata = serialdata;
            state.serialreg = serialreg;
            state.keydata1 = 0xF;
            state.keydata2 = 0xF;
            state.ie = ie;
            state.iflag = iflag;
            state.lastcycles = lastcycles;
            return state;
        }

        private void LoadSaveState(Savestate state)
        {
            _cpu.LoadSaveState(state);
            _ppu.LoadSaveState(state);
            _apu.LoadSaveState(state);
            Array.Copy(state.WRAM, WRAM, WRAM.Length);
            Array.Copy(state.ZeroPage, ZeroPage, ZeroPage.Length);
            Array.Copy(state.ioregisters, ioregisters, ioregisters.Length);
            wramBankNo = state.wramBankNo;
            p1 = state.p1;
            div = state.div;
            tima = state.tima;
            tma = state.tma;
            tac = state.tac;
            dma = state.dma;
            gbcMode = state.gbcMode;
            doubleSpeed = state.doubleSpeed;
            prepareSpeedSwitch = state.prepareSpeedSwitch;
            serialdata = state.serialdata;
            serialreg = state.serialreg;
            keydata1 = 0xF;
            keydata2 = 0xF;
            ie = state.ie;
            iflag = state.iflag;
            lastcycles = state.lastcycles;
        }
    }
}
