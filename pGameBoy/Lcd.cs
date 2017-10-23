using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class Lcd
    {

        private Core _core;

        private byte[] oamRam = new byte[0x100];

        const byte LCD_width = 160;
        const byte LCD_heigth = 144;
        const byte Vblank = 10;


        //Lcd display registers constants LCDC
        const byte BgDisplayOn = 0x01;
        const byte ObjOn = 0x02;
        const byte ObjBlockSize = 0x04;
        const byte BgTileArea = 0x08;
        const byte BgTileData = 0x10;
        const byte WindowOn = 0x20;
        const byte WindowTileArea = 0x40;
        const byte LcdControlOperation = 0x80;

        //interrupt constants
        const byte vblank_const = 0x01, LCDC_const = 0x02, Timeroverflow_const = 0x04, Serial_const = 0x08, negativeedge_const = 0x10;

        //Stat register constans
        const byte InHblank = 0x00;
        const byte InVblank = 0x01;
        const byte SearchingOam = 0x02;
        const byte TransferingData = 0x03;
        const byte LyMatchFlag = 0x04;
        const byte Mode0Interrupt = 0x08;
        const byte Mode1Interrupt = 0x10;
        const byte Mode2Interrupt = 0x20;
        const byte LycInterrupt = 0x40;

        //Lcd registers
        private byte stat; //lcdstatus
        private byte wy; //window x/y position
        private byte wx;//window x/y position
        private byte scy; //Scroll Y
        private byte scx; //Scroll X
        private byte ly;  //Lcdc y coordinate
        private byte lyc; //Lycompare
        private byte lcdc; //lcdcontrol

        private byte bgp; //Pallette data, what shade of grey
        private byte obp0; //Object pallet data
        private byte obp1;//Object pallet data


        private int lcd_clockcount;
        private bool InVblank_interrupt;

        private byte[] _framebuffer = new byte[160 * 144];
        private bool _frameready = false;

        private byte[] bgpallete = new byte[]
        {
            0,
            1,
            2,
            3
        };
        private byte[] obj0pallete = new byte[]
        {
            0,
            1,
            2,
            3
        };
        private byte[] obj1pallete = new byte[]
        {
            0,
            1,
            2,
            3
        };


        public byte[] OAM { get { return oamRam; } }
        public byte[] Framebuffer { get { return _framebuffer; } }
        public bool Frameready { get { return _frameready; } set { _frameready = value; } }

        public Lcd(Core core)
        {
            _core = core;
        }
        public void LcdTick()
        {
            if ((lcdc & LcdControlOperation) != 0)
            {


                switch (stat & 0x3)
                {
                    case SearchingOam:
                        if (lcd_clockcount >= 80)
                        {
                            stat &= 0xFC;  //Clear last 2 bits in stat
                            stat |= TransferingData;
                        }
                        break;
                    case TransferingData:
                        if (lcd_clockcount >= 172)
                        {
                            stat &= 0xFC;  //Clear last 2 bits in stat
                            stat |= InHblank;
                            if ((stat & Mode0Interrupt) == Mode0Interrupt)
                            {
                                _core.SetIflag(LCDC_const);
                            }
                            RenderScanline();
                        }
                        break;
                    case InHblank: //Hblank
                        if (lcd_clockcount >= 456)
                        {
                            lcd_clockcount = 0;
                            ly++;
                            CheckLyc();
                            if (ly > 143) // Enter vblank
                            {

                                stat &= 0xFC;  //Clear last 2 bits in stat
                                stat |= InVblank;
                                InVblank_interrupt = true;
                                _frameready = true;
                                //Todo render one frame;
                            }
                            else
                            {

                                stat &= 0xFC;  //Clear last 2 bits in stat
                                stat |= SearchingOam;
                                if ((stat & Mode2Interrupt) == Mode2Interrupt)
                                {
                                    _core.SetIflag(LCDC_const);
                                }

                            }

                        }
                        break;
                    case InVblank: //Vblank
                        if (InVblank_interrupt && lcd_clockcount >= 24)
                        {
                            _core.SetIflag(vblank_const);
                            InVblank_interrupt = false;

                            if ((stat & Mode1Interrupt) == Mode1Interrupt)
                            {
                                _core.SetIflag(LCDC_const);
                            }
                        }
                        if (lcd_clockcount >= 456)
                        {

                            lcd_clockcount = 0;
                            ly++;
                            CheckLyc();
                            if (ly > 153)
                            {
                                ly = 0;
                                CheckLyc();
                                stat &= 0xFC;  //Clear last 2 bits in stat
                                stat |= SearchingOam;
                                if ((stat & Mode2Interrupt) == Mode2Interrupt)
                                {
                                    _core.SetIflag(LCDC_const);
                                }

                            }
                        }
                        break;

                }
                lcd_clockcount++;
            }
        }
        public void WriteLcdRegister(ushort address, byte data)
        {
            switch (address & 0xff)
            {
                case 0x40:
                    byte isscreenon = (byte)(lcdc >> 7);
                    byte turnscreenon = (byte)(data >> 7);
                    if (isscreenon != 0 && turnscreenon == 0)
                    {
                        ly = 0;
                        var mask = ~0x03;
                        stat &= (byte)(mask);
                        lcd_clockcount = 0;
                        for (int i = 0; i < _framebuffer.Length; i++)
                        {
                           _framebuffer[i] = 0;
                        }
                    }
                    else if (isscreenon == 0 && turnscreenon != 0)
                    {
                        lcd_clockcount = 0;
                        CheckLyc();
                    }

                    lcdc = data; break;

                case 0x41:
                    byte temp1 = (byte)(data & ~0x7);
                    temp1 |= (byte)(stat & 0x07);
                    stat = temp1;
                    break;
                case 0x42: scy = data; break;
                case 0x43: scx = data; break;
                case 0x44: break;
                case 0x45: lyc = data; break;
                case 0x47: bgp = data; UpdatePalette(ref bgpallete, bgp); break;
                case 0x48: obp0 = data; UpdatePalette(ref obj0pallete, obp0); break;
                case 0x49: obp1 = data; UpdatePalette(ref obj1pallete, obp1); break;
                case 0x4A: wy = data; break;
                case 0x4B: wx = data; break;
            }

        }
        public byte ReadLcdRegister(ushort address)
        {
            switch (address & 0xff)
            {
                case 0x40: return lcdc;
                case 0x41: return stat;
                case 0x42: return scy;
                case 0x43: return scx;
                case 0x44: return ly;
                case 0x45: return lyc;
                case 0x46: return 0;
                case 0x47: return bgp;
                case 0x48: return obp0;
                case 0x49: return obp1;
                case 0x4A: return wy;
                case 0x4B: return wx;
            }
            return 0;
        }
        private void LcdInterrupt()
        {


        }
        private void RenderScanline()
        {
            int windowtilemap = (lcdc & WindowTileArea) == WindowTileArea ? 0x9C00 : 0x9800;
            int bgtiledata = (lcdc & BgTileData) == BgTileData ? 0x8000 : 0x8800;
            int bgtilemap = (lcdc & BgTileArea) == BgTileArea ? 0x9C00 : 0x9800;
            bool largesprites = (lcdc & ObjBlockSize) == ObjBlockSize ? true : false;

            if ((lcdc & BgDisplayOn) != BgDisplayOn)
            {
                for (int i = 0; i < _framebuffer.Length; i++)
                {
                    _framebuffer[i] = 0;
                   
                }
            }

            if ((lcdc & BgDisplayOn) == BgDisplayOn)
            {
                for (int i = 0; i < LCD_width; i++)
                {
                    int currentx = i + scx > 255 ? i + scx - 256 : i + scx;
                    int currenty = ly + scy > 255 ? ly + scy - 256 : ly + scy;
                    int currenttileinfo = (currentx / 8) + ((currenty / 8) * 32);
                    int currenttilerow = (ly + scy) % 8;

                    int tileinfo = _core.ReadMem((ushort)(bgtilemap + currenttileinfo));
                    if ((bgtiledata) == 0x8800)
                    {
                        if (tileinfo > 127)
                        {
                            tileinfo -= 128;
                        }
                        else
                        {
                            tileinfo += 128;
                        }
                    }
                    tileinfo *= 0x10;

                    byte tiledata1 = _core.ReadMem((ushort)(bgtiledata + tileinfo + (currenttilerow * 2)));
                    byte tiledata2 = _core.ReadMem((ushort)(bgtiledata + tileinfo + (currenttilerow * 2) + 1));
                    int pix = 7 - ((i + scx) % 8);
                    byte data1 = (byte)(((tiledata1 & (1 << pix)) >> pix));
                    byte data2 = (byte)((((tiledata2 & (1 << pix)) >> pix) << 1));
                    byte pixel = (byte)(data1 | data2);
                    int pixelplace = (LCD_width * ly) + i;

                    _framebuffer[pixelplace] = bgpallete[pixel];

                }
            }
            if ((lcdc & WindowOn) != 0)
            {
                for (int i = 0; i < LCD_width; i++)
                {
                    bool render = false;
                    if (wx <= 166 && ly >= wy && i >= (wx - 7)) render = true;
                    if (render)
                    {
                        int wxx = wx - 7;
                        int currentx = ((i - wxx) / 8);
                        int currenty = ((ly - wy) / 8);
                        int currenttileinfo = currentx + (currenty * 32);
                        int currenttilerow = (ly - wy) % 8;

                        int tileinfo = _core.ReadMem((ushort)(windowtilemap + currenttileinfo));
                        if ((bgtiledata) == 0x8800)
                        {
                            if (tileinfo > 127)
                            {
                                tileinfo -= 128;
                            }
                            else
                            {
                                tileinfo += 128;
                            }
                        }
                        tileinfo *= 0x10;

                        byte tiledata1 = _core.ReadMem((ushort)(bgtiledata + tileinfo + (currenttilerow * 2)));
                        byte tiledata2 = _core.ReadMem((ushort)(bgtiledata + tileinfo + (currenttilerow * 2) + 1));
                        int pix = 7 - ((i - wxx) % 8);
                        byte data1 = (byte)(((tiledata1 & (1 << pix)) >> pix));
                        byte data2 = (byte)((((tiledata2 & (1 << pix)) >> pix) << 1));
                        byte pixel = (byte)(data1 | data2);
                        int pixelplace = (LCD_width * ly ) + i;

                        _framebuffer[pixelplace] = bgpallete[pixel];
                    }
                }
            }
            if ((lcdc & ObjOn) != 0)
            {
                for (int i = 0x9F; i > 0; i -= 4)
                {
                    int ypos = oamRam[i - 3];
                    int xpos = oamRam[i - 2];
                    int tilepattern = oamRam[i - 1];
                    byte flags = oamRam[i];
                    if (largesprites) tilepattern &= ~1;
                    bool priority = (flags & 0x80) != 0 ? true : false;
                    bool yfliped = (flags & 0x40) != 0 ? true : false;
                    bool xfliped = (flags & 0x20) != 0 ? true : false;
                    tilepattern *= 0x10;
                    bool rendersprite = true;
                    bool renderpixel = true;
                    if (ypos == 0 | xpos == 0)
                        rendersprite = false;

                    ypos -= 16;
                    xpos -= 8;

                    if (ypos > 143)
                        rendersprite = false;
                    if (ly < ypos)
                        rendersprite = false;
                    if (rendersprite)
                    {

                        int tiley = largesprites ? ((ly) % 16) : ((ly) % 8);
                        if (yfliped) tiley = largesprites ? 15 - (ly % 16) : 7 - (ly % 8);
                        int y = largesprites ? ypos + (ly % 16) : ypos + (ly % 8);
                        byte tiledata1 = _core.ReadMem((ushort)(tilepattern + 0x8000 + (tiley * 2)));
                        byte tiledata2 = _core.ReadMem((ushort)(tilepattern + 0x8000 + 1 + (tiley * 2)));

                        for (int x = 0; x < 8; x++)
                        {
                            if ((xpos + x) < LCD_width && y < LCD_heigth && y >= 0 && (xpos + x) >= 0)
                            {
                                int pix;
                                if (xfliped) { pix = x; }
                                else { pix = (7 - x); }
                                byte data1 = (byte)(((tiledata1 & (1 << pix)) >> pix));
                                byte data2 = (byte)((((tiledata2 & (1 << pix)) >> pix) << 1));
                                byte pixel = (byte)(data1 | data2);
                                int pixelplace = (LCD_width * y) + (xpos + x) ;
                                renderpixel = true;

                                if (pixel == 0) renderpixel = false;
                                if (priority && _framebuffer[pixelplace] != bgpallete[0]) { renderpixel = false; }
                                if (renderpixel)
                                {
                                    byte tempPalette = (flags & 0x10) == 0x10 ? obj1pallete[pixel] : obj0pallete[pixel];
                                    _framebuffer[pixelplace] = tempPalette;

                                }
                            }
                        }
                    }


                }
            }




        }
        public void UpdatePalette(ref byte[] palette, byte data)
        {
            for (int i = 0; i < 4; i++)
            {
                int temp = data >> (i * 2);
                palette[i] = (byte)(temp & 0x3);
            }


        }
        public void CheckLyc()
        {
            if (ly == lyc)
            {
                stat |= LyMatchFlag;
                if ((stat & LycInterrupt) == LycInterrupt)
                {
                    _core.SetIflag(LCDC_const);
                }
            }
            else
            {
                var bitmask = ~LyMatchFlag;
                stat &= (byte)bitmask;
            }
        }
        public void ResetLcd()
        {
            lcdc = 0x91;
            scy = 0;
            scx = 0;
            lyc = 0;
            bgp = 0xFC;
            obp0 = 0xFF;
            obp1 = 0xFF;
            wy = 0;
            wx = 0;
            for (int i = 0; i < _framebuffer.Length; i++)
            {
                _framebuffer[i] = 0;
            }

        }
    }
}
