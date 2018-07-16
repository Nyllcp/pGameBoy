using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class PPU
    {

        private Core _core;

        private byte[] oamRam = new byte[0x100];
        private byte[,] VRAM = new byte[2, 0x2000];
        private byte vramBankNo = 0;

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

        //gbc registers
        private byte bgPaletteIndex = 0;
        private byte[] gbcBgPalette = new byte[0x40];
        private byte objPaletteIndex = 0;
        private byte[] gbcObjPalette = new byte[0x40];
        private byte hdmaSourceHighByte = 0;
        private byte hdmaSourceLowByte = 0;
        private byte hdmaDestHighByte = 0;
        private byte hdmaDestLowByte = 0;
        private byte hdmaLenghtCounter = 0;
        private bool hdmaDuringHblank = false;
        private ushort hdmaSource = 0;
        private ushort hdmaDest = 0;


        private int lcd_clockcount;
        private bool InVblank_interrupt;

        private uint[] _framebufferRGB = new uint[160 * 144];
        private bool _frameready = false;
        private bool gbcMode = false;

        private byte[] bgPallete = new byte[]
        {
            0,
            1,
            2,
            3
        };
        private byte[] obj0Pallete = new byte[]
        {
            0,
            1,
            2,
            3
        };
        private byte[] obj1Pallete = new byte[]
        {
            0,
            1,
            2,
            3
        };  

        public byte[] OAM { get { return oamRam; } }
        public uint[] FramebufferRGB { get { return _framebufferRGB; } }
        public bool Frameready { get { return _frameready; } set { _frameready = value; } }

        public PPU(Core core)
        {
            _core = core;
            gbcMode = _core.GbcMode;
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
                            if (hdmaDuringHblank)
                            {
                                if ((hdmaLenghtCounter & 0x7F) > 0)
                                {
                                    for (int i = 0; i < 0x10; i++)
                                    {
                                        WriteVram((ushort)(hdmaDest + i), _core.ReadMem((ushort)(hdmaSource + i)));
                                    }
                                    hdmaDest += 0x10;
                                    hdmaSource += 0x10;
                                    int temp = (hdmaLenghtCounter - 1) & 0x7F;
                                    if (temp == 0)
                                    {
                                        hdmaLenghtCounter = 0;
                                        hdmaDuringHblank = false;
                                    }
                                    else
                                    {
                                        hdmaLenghtCounter--;
                                    }
                                }
                                else
                                {
                                    hdmaDuringHblank = false;
                                    hdmaLenghtCounter = 0;
                                }

                            }
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
                        for (int i = 0; i < _framebufferRGB.Length; i++)
                        {
                            _framebufferRGB[i] = 0xFF000000;
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
                case 0x47: bgp = data; UpdatePalette(ref bgPallete, bgp); break;
                case 0x48: obp0 = data; UpdatePalette(ref obj0Pallete, obp0); break;
                case 0x49: obp1 = data; UpdatePalette(ref obj1Pallete, obp1); break;
                case 0x4A: wy = data; break;
                case 0x4B: wx = data; break;
                case 0x4F: vramBankNo = (byte)(data & 1);break;
                case 0x51: hdmaSourceHighByte = data; break;
                case 0x52: hdmaSourceLowByte = data; break;
                case 0x53: hdmaDestHighByte = data; break;
                case 0x54: hdmaDestLowByte = data; break;
                case 0x55: hdmaLenghtCounter = data; InitHdma(); break;
                case 0x68: bgPaletteIndex = data; break;
                case 0x69: UpdateGbcPalette(true, data); break;
                case 0x6A: objPaletteIndex = data; break;
                case 0x6B: UpdateGbcPalette(false, data); break;
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
                case 0x4F: return vramBankNo;
                case 0x51: return hdmaSourceHighByte;
                case 0x52: return hdmaSourceLowByte;
                case 0x53: return hdmaDestHighByte;
                case 0x54: return hdmaDestLowByte;
                case 0x55: return hdmaLenghtCounter;
                case 0x68: return bgPaletteIndex;
                case 0x69: return gbcBgPalette[bgPaletteIndex & 0x3F];
                case 0x6A: return objPaletteIndex;
                case 0x6B: return gbcObjPalette[objPaletteIndex & 0x3F];
            }
            return 0;
        }
        private void RenderScanline()
        {
            int windowtilemap = (lcdc & WindowTileArea) == WindowTileArea ? 0x9C00 : 0x9800;
            int bgtiledata = (lcdc & BgTileData) == BgTileData ? 0x8000 : 0x8800;
            int bgtilemap = (lcdc & BgTileArea) == BgTileArea ? 0x9C00 : 0x9800;
            bool largesprites = (lcdc & ObjBlockSize) == ObjBlockSize ? true : false;

            int[,] scanlinebuffer = new int[2, LCD_width];

            if ((lcdc & BgDisplayOn) != BgDisplayOn && !gbcMode)
            {
                for (int i = 0; i < LCD_width; i++)
                {
                    {
                        _framebufferRGB[i + (ly * LCD_width)] = Palette.BGB[0];
                    }
                }
            }

            if ((lcdc & BgDisplayOn) == BgDisplayOn || gbcMode)
            {
                for (int i = 0; i < LCD_width; i++)
                {
                    int currentx = i + scx > 255 ? i + scx - 256 : i + scx;
                    int currenty = ly + scy > 255 ? ly + scy - 256 : ly + scy;
                    int currenttileinfo = (currentx / 8) + ((currenty / 8) * 32);
                    int currenttilerow = (ly + scy) % 8;
                    int tileinfo = ReadVramBank(0, ((ushort)(bgtilemap + currenttileinfo)));

                    //gbcstuff
                    int bgattribute = ReadVramBank(1, ((ushort)(bgtilemap + currenttileinfo)));
                    int bgpalleteno = bgattribute & 0x7;
                    int vrambank = (bgattribute >> 3 & 1);
                    bool flipx = ((bgattribute >> 5) & 1) != 0;
                    bool flipy = ((bgattribute >> 6) & 1) != 0;
                    bool bgpriority = ((bgattribute >> 7) & 1) != 0;

                    
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
                    if(flipy)
                    {
                        currenttilerow = 7 - currenttilerow;
                    }
                    byte tiledata1 = ReadVramBank(vrambank, (ushort)(bgtiledata + tileinfo + (currenttilerow * 2)));
                    byte tiledata2 = ReadVramBank(vrambank, ((ushort)(bgtiledata + tileinfo + (currenttilerow * 2) + 1)));
                    int pix = 7 - ((i + scx) % 8);
                    if(flipx)
                    {
                        pix = ((i + scx) % 8);
                    }
                    byte data1 = (byte)(((tiledata1 & (1 << pix)) >> pix));
                    byte data2 = (byte)((((tiledata2 & (1 << pix)) >> pix) << 1));
                    byte pixel = (byte)(data1 | data2);

                    int pixelplace =  i;
                    if (gbcMode)
                    {
                        bgpalleteno |= 1 << 6;
                        if (bgpriority) bgpalleteno |= 1 << 7;
                        scanlinebuffer[0, pixelplace] = pixel;
                        scanlinebuffer[1, pixelplace] = bgpalleteno;
                    }
                    else
                    {
                        scanlinebuffer[0, pixelplace] = bgPallete[pixel];
                    }

                }
            }
            if ((lcdc & WindowOn) != 0)
            {
                for (int i = 0; i < LCD_width; i++)
                {
                    bool render = false;
                    int wxx = wx - 7;
                    if (ly >= wy && wxx < LCD_width && i >= wxx) render = true;
                    if (render)
                    { 
                        int currentx = ((i - wxx) / 8);
                        int currenty = ((ly - wy) / 8);
                        int currenttileinfo = currentx + (currenty * 32);
                        int currenttilerow = (ly - wy) % 8;
                        int tileinfo = ReadVramBank(0,((ushort)(windowtilemap + currenttileinfo)));

                        //gbcstuff
                        int bgattribute = ReadVramBank(1, ((ushort)(windowtilemap + currenttileinfo)));
                        int bgpalleteno = bgattribute & 0x7;
                        int vrambank = (bgattribute >> 3 & 1);
                        bool flipx = ((bgattribute >> 5) & 1) != 0;
                        bool flipy = ((bgattribute >> 6) & 1) != 0;
                        bool bgpriority = ((bgattribute >> 7) & 1) != 0;
                        if (flipy && gbcMode)
                        {
                            currenttilerow = 7 - currenttilerow;
                        }


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

                        byte tiledata1 = ReadVramBank(vrambank, ((ushort)(bgtiledata + tileinfo + (currenttilerow * 2))));
                        byte tiledata2 = ReadVramBank(vrambank, ((ushort)(bgtiledata + tileinfo + (currenttilerow * 2) + 1)));
                        int pix = 7 - ((i - wxx) % 8);
                        if (flipx && gbcMode)
                        {
                            pix = ((i - wxx) % 8);
                        }
                        byte data1 = (byte)(((tiledata1 & (1 << pix)) >> pix));
                        byte data2 = (byte)((((tiledata2 & (1 << pix)) >> pix) << 1));
                        byte pixel = (byte)(data1 | data2);

                        int pixelplace = i;
                        if (gbcMode)
                        {
                            bgpalleteno |= 1 << 6;
                            if (bgpriority) bgpalleteno |= 1 << 7;
                            scanlinebuffer[0, pixelplace] = pixel;
                            scanlinebuffer[1, pixelplace] = bgpalleteno;
                        }
                        else
                        {
                            scanlinebuffer[0, pixelplace] = bgPallete[pixel];
                        }
                    }
                }
            }
            if ((lcdc & ObjOn) != 0)
            {
                int spritesOnScanline = 0;
                for (int i = 0; i < 0x9F; i += 4)
                {
                    int ypos = oamRam[i];
                    int xpos = oamRam[i + 1];
                    int tilepattern = oamRam[i + 2];
                    byte flags = oamRam[i + 3];
                    if (largesprites) tilepattern &= ~1;
                    bool priority = (flags & 0x80) != 0 ? true : false;
                    bool yfliped = (flags & 0x40) != 0 ? true : false;
                    bool xfliped = (flags & 0x20) != 0 ? true : false;
                    //gbcStuff

                    int objpalette = 0; 
                    int vrambank = 0;  
                    if(gbcMode)
                    {
                        objpalette = flags & 0x7;
                        vrambank = (flags >> 3 & 1);
                    }


                    tilepattern *= 0x10;
                    bool rendersprite = true;

                    if (ypos == 0 || ypos >= 160) { rendersprite = false; } 
                           
                    ypos -= 16;
                    
                    if (ly < ypos || ly > ypos + (largesprites ? 15 : 7)) { rendersprite = false; } 
                    else { spritesOnScanline++; }

                    if(xpos >= 168 || xpos == 0) { rendersprite = false; }

                    xpos -= 8;

                    if (spritesOnScanline > 10) { rendersprite = false; } //max 10 spries per scanline
                    if (rendersprite)
                    {
                        int tiley = ly - ypos;
                        if (yfliped) tiley = largesprites ? 15 - (ly - ypos) : 7 - (ly - ypos); 
                        byte tiledata1 = ReadVramBank(vrambank, ((ushort)(tilepattern + 0x8000 + (tiley * 2))));
                        byte tiledata2 = ReadVramBank(vrambank, ((ushort)(tilepattern + 0x8000 + 1 + (tiley * 2))));
                        bool renderpixel = true;

                        for (int x = 0; x < 8; x++)
                        {
                            if ((xpos + x) < LCD_width && (xpos + x) >= 0)
                            {
                                int pix;
                                if (xfliped) { pix = x; }
                                else { pix = (7 - x); }
                                byte data1 = (byte)(((tiledata1 & (1 << pix)) >> pix));
                                byte data2 = (byte)((((tiledata2 & (1 << pix)) >> pix) << 1));
                                byte pixel = (byte)(data1 | data2);
                                int pixelplace = (xpos + x);
                                renderpixel = true;
                                if (pixel == 0) renderpixel = false;

                                if (priority && scanlinebuffer[0, pixelplace] != 0) { renderpixel = false; }
                                if (gbcMode)
                                {
                                    if (((scanlinebuffer[1, pixelplace] >> 7) & 1) != 0) { renderpixel = false; }
                                    if((lcdc & BgDisplayOn) == 0) { renderpixel = true; }
                                }
                               
                                if (renderpixel)
                                {
                                    if (gbcMode)
                                    {
                                        
                                        scanlinebuffer[0, pixelplace] = pixel;
                                        scanlinebuffer[1, pixelplace] = objpalette;
                                    }
                                    else
                                    {
                                        scanlinebuffer[0, pixelplace] = (flags & 0x10) == 0x10 ? obj1Pallete[pixel] : obj0Pallete[pixel];
                                    }

                                }
                            }
                        }
                    }


                }
            }

            for(int i = 0; i < LCD_width; i++)
            {
                if (gbcMode)
                {
                    _framebufferRGB[i + (ly * LCD_width)] = Get32bitRGB(scanlinebuffer[0, i], scanlinebuffer[1, i]);
                }
                else
                {
                    _framebufferRGB[i + (ly * LCD_width)] = Get32bitRGB(scanlinebuffer[0, i], 0);
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
            gbcMode = _core.GbcMode;
            for (int i = 0; i < _framebufferRGB.Length; i++)
            {
                _framebufferRGB[i] = 0xFF000000;
            }

        }
        private void UpdateGbcPalette(bool bg, byte data)
        {
            if (bg)
            {
                gbcBgPalette[(bgPaletteIndex & 0x3F)] = data;
                if (((bgPaletteIndex >> 7) & 1) != 0)
                {
                    bgPaletteIndex++;
                    bgPaletteIndex |= 0x80;
                }
            }
            else
            {
                gbcObjPalette[(objPaletteIndex & 0x3F)] = data;
                if (((objPaletteIndex >> 7) & 1) != 0)
                {
                    objPaletteIndex++;
                    objPaletteIndex |= 0x80;
                }
            }
        }
        
        public void WriteVram(ushort address, byte data)
        {
            VRAM[vramBankNo, address & 0x1FFF] = data;
        }
        public byte ReadVram(ushort address)
        {
            return VRAM[vramBankNo, address & 0x1FFF];
        }

        private byte ReadVramBank(int bank, ushort address)
        {
            if (bank > 1) bank = 1;
            return VRAM[bank, address & 0x1FFF];
        }

        private void InitHdma()
        {
            hdmaSource = (ushort)(hdmaSourceHighByte << 8 | (hdmaSourceLowByte & 0xF0));
            hdmaDest = (ushort)(hdmaDestHighByte << 8 | (hdmaDestLowByte & 0xF0));
            if (hdmaLenghtCounter >> 7 == 0)
            {
                int lenght = 16 + (hdmaLenghtCounter * 16);
                for (int i = 0; i < lenght; i++)
                {
                    WriteVram((ushort)(hdmaDest + i), _core.ReadMem((ushort)(hdmaSource + i)));
                }
                hdmaLenghtCounter = 0;
            }
            else
                hdmaDuringHblank = true;
        }

        private uint Get32bitRGB(int pixel,int palette)
        {
            if(gbcMode)
            {
                int high;
                int low;
                if (((palette >> 6) & 1) != 0)
                {
                    low = gbcBgPalette[((palette & 0x7) * 8) + (pixel * 2)];
                    high = gbcBgPalette[((palette & 0x7) * 8) + ((pixel * 2) + 1)];
                }
                else
                {
                    low = gbcObjPalette[((palette & 0x7) * 8) + (pixel * 2)];
                    high = gbcObjPalette[((palette & 0x7) * 8) + ((pixel * 2) + 1)];
                }


                uint value = 0xFF000000;
                uint red = (uint)(low & 0x1F) * 8;
                uint green = (uint)((low >> 5 | (high & 0x3) << 3) & 0x1F) * 8;
                uint blue = (uint)((high >> 2) & 0x1F) * 8;
                value |= (red | (green << 8) | (blue << 16));
                return value;
            }
            else
            {
                return Palette.BGB[pixel];
            }
         
        }

        public void WriteSaveState(ref Savestate state)
        {
            Array.Copy(oamRam, state.oamRam, oamRam.Length);
            Array.Copy(VRAM, state.VRAM, VRAM.Length);
            Array.Copy(gbcBgPalette, state.gbcBgPalette, gbcBgPalette.Length);
            Array.Copy(gbcObjPalette, state.gbcObjPalette, gbcObjPalette.Length);
            Array.Copy(_framebufferRGB, state._framebufferRGB, _framebufferRGB.Length);

            state.vramBankNo = vramBankNo;
            state.stat = stat;
            state.wy = wy;
            state.wx = wx;
            state.scy = scy;
            state.scx = scx;
            state.ly = ly;
            state.lyc = lyc;
            state.lcdc = lcdc;
            state.bgp = bgp;
            state.obp0 = obp0;
            state.obp1 = obp1;
            state.bgPaletteIndex = bgPaletteIndex;
            state.objPaletteIndex = objPaletteIndex;
            state.hdmaSourceHighByte = hdmaSourceHighByte;
            state.hdmaSourceLowByte = hdmaSourceLowByte;
            state.hdmaDestHighByte = hdmaDestHighByte;
            state.hdmaDestLowByte = hdmaDestLowByte;
            state.hdmaLenghtCounter = hdmaLenghtCounter;
            state.hdmaDuringHblank = hdmaDuringHblank;
            state.hdmaSource = hdmaSource;
            state.hdmaDest = hdmaDest;
            state.lcd_clockcount = lcd_clockcount;
            state.InVblank_interrupt = InVblank_interrupt;
            state._frameready = _frameready;
        }
        public void LoadSaveState(Savestate state)
        {
            Array.Copy(state.oamRam, oamRam, oamRam.Length);
            Array.Copy(state.VRAM, VRAM, VRAM.Length);
            Array.Copy(state.gbcBgPalette, gbcBgPalette, gbcBgPalette.Length);
            Array.Copy(state.gbcObjPalette, gbcObjPalette, gbcObjPalette.Length);
            Array.Copy(state._framebufferRGB, _framebufferRGB, _framebufferRGB.Length);
         
            vramBankNo = state.vramBankNo;
            stat = state.stat;
            wy = state.wy;
            wx = state.wx;
            scy = state.scy;
            scx = state.scx;
            ly = state.ly;
            lyc = state.lyc;
            lcdc = state.lcdc;
            bgp =  state.bgp;
            obp0 = state.obp0;
            obp1 = state.obp1;
            UpdatePalette(ref bgPallete, bgp);
            UpdatePalette(ref obj0Pallete, obp0);
            UpdatePalette(ref obj1Pallete, obp1);
            bgPaletteIndex = state.bgPaletteIndex;
            objPaletteIndex = state.objPaletteIndex;
            hdmaSourceHighByte = state.hdmaSourceHighByte;
            hdmaSourceLowByte = state.hdmaSourceLowByte;
            hdmaDestHighByte = state.hdmaDestHighByte;
            hdmaDestLowByte = state.hdmaDestLowByte;
            hdmaLenghtCounter = state.hdmaLenghtCounter;
            hdmaDuringHblank = state.hdmaDuringHblank;
            hdmaSource = state.hdmaSource;
            hdmaDest = state.hdmaDest;
            lcd_clockcount = state.lcd_clockcount;
            InVblank_interrupt = state.InVblank_interrupt;
            _frameready = state._frameready;
        }
    }
}
