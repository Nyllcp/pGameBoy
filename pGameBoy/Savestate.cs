using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    [Serializable()]
    public class Savestate
    {
        public Savestate()
        {
            for(int i = 0; i < SoundChannels.Length; i++)
            {
                SoundChannels[i] = new SoundChannel();
            }
        }
        public SoundChannel[] SoundChannels = new SoundChannel[4];
        //Core states
        public byte[,] WRAM = new byte[8, 0x1000];
        public byte[] ZeroPage = new byte[0x80];
        public byte wramBankNo = 1;
        public byte p1; //Joypad
        public ushort div; // divider register
        public byte tima; //Timer counter
        public byte tma; //Timer modulo
        public byte tac; //Timer Control
        public byte dma; //Dma adress
        public bool gbcMode = false;
        public bool doubleSpeed = false;
        public bool prepareSpeedSwitch = false;
        public byte serialdata;
        public byte serialreg;
        public byte[] ioregisters = new byte[0x100];
        public byte keydata1 = 0xF;
        public byte keydata2 = 0xF;
        public byte ie = 0xE0; //Interrupt enable
        public byte iflag = 0xE0; //Interruptflag
        public int lastcycles;

        //PPU states
        public byte[] oamRam = new byte[0x100];
        public byte[,] VRAM = new byte[2, 0x2000];
        public byte vramBankNo = 0;
        public byte stat; //lcdstatus
        public byte wy; //window x/y position
        public byte wx;//window x/y position
        public byte scy; //Scroll Y
        public byte scx; //Scroll X
        public byte ly;  //Lcdc y coordinate
        public byte lyc; //Lycompare
        public byte lcdc; //lcdcontrol
        public byte bgp; //Pallette data, what shade of grey
        public byte obp0; //Object pallet data
        public byte obp1;//Object pallet data
        public byte bgPaletteIndex = 0;
        public byte[] gbcBgPalette = new byte[0x40];
        public byte objPaletteIndex = 0;
        public byte[] gbcObjPalette = new byte[0x40];
        public byte hdmaSourceHighByte = 0;
        public byte hdmaSourceLowByte = 0;
        public byte hdmaDestHighByte = 0;
        public byte hdmaDestLowByte = 0;
        public byte hdmaLenghtCounter = 0;
        public bool hdmaDuringHblank = false;
        public ushort hdmaSource = 0;
        public ushort hdmaDest = 0;
        public int lcd_clockcount;
        public bool InVblank_interrupt;
        public uint[] _framebufferRGB = new uint[160 * 144];
        public bool _frameready = false;


        //CPU States
        public int cycles;
        public ushort sp;
        public ushort pc;
        public byte a;
        public byte b;
        public byte c;
        public byte d;
        public byte e;
        public byte f;
        public byte h;
        public byte l;
        public bool halt = false;
        public bool stop = false;
        public bool interruptsenabled = false;

        //APU
        public int soundCycles = 0;
        public int frameSequenceStep = 0;
        public byte[] Samples = new byte[2048]; // Abit more then a frame worth of samples. 44100 / 60 = 735. 2 bytes per sample 1470 bytes..
        public int NumberOfSamples = 0;

        public byte channelCtrlReg = 0;
        public byte soundOutputReg = 0;
        public bool soundEnabled = true;

        [Serializable()]
        public class SoundChannel
        {
            public SoundChannel() { }
            //Squarechannel + shared
            public byte Reg0 = 0;
            public byte Reg1 = 0;
            public byte Reg2 = 0;
            public byte Reg3 = 0;
            public byte Reg4 = 0;
            public bool ChannelEnable = false;
            public byte SweepPeriod = 0;
            public bool Negate = false;
            public byte Shift = 0;
            public byte Duty = 0;
            public byte LengthLoad = 0;
            public byte StartingVolume = 0;
            public bool EnvelopeAddMode = false;
            public byte EnvelopePeriod = 0;
            public bool LengthEnable = false;
            public int CurrentDuty = 0;
            public int Sample = 0;
            public int Frequency = 0;
            public bool SweepEnabled = false;
            public int ShadowFreq = 0;
            public int SweepStep = 0;
            public int Cycles = 0;
            public bool Envelope_enabled;

            //Wave Channel
            public bool DacPower = false;
            public byte[] WaveTable = new byte[0X10];
            public int wavePos = 0;
            public int Volume = 0;

            //Noise Channel
            public bool WidthOfLFSR = false;
            public byte DivisorCode = 0;
            public int LFSR = 0;
        }

   



    }
}
