using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace pGameBoy
{
    class Cart
    {
        private BinaryReader _reader;
        private MemoryStream _ms;

        private byte[] prgROM;
        private byte[] saveRAM;
        private byte[] batteryRAM = new byte[0x200];
        private byte cartType;
        private byte romSize;
        private byte ramSize;
        private byte bankNo;
        private bool ramMode;
        private bool enableCartRAM;
        private int romOffset = 0x4000;
        private int ramOffset = 0;
        private int prgRomSize = 0;

        //Bools for cart types
        private bool cartMbc1 = false;
        private bool cartRam = false;
        private bool cartBattery = false;
        private bool cartMbc2 = false;

        private string currentRomName;
        private string saveFilename;

        public string CurrentRomName { get { return currentRomName; } }

        private static readonly int[] SaveSize = new int[]
        {
            0x800, 0x2000,0x8000, 0x20000, 0x10000
        };

        public Cart() { }
        ~Cart()
        {
            if(cartBattery)
            {
                WriteSaveFile();
            }
        }
        public bool LoadRom(string filename)
        {
            if(Path.GetExtension(filename) == ".zip")
            {
                _ms = new MemoryStream();
                using (var _zip = ZipFile.OpenRead(filename))
                {
                    foreach(var entry in _zip.Entries)
                    {
                        if(Path.GetExtension(entry.Name) == ".gb")
                        {
                            using (var stream = entry.Open())
                            {
                                stream.CopyTo(_ms);
                            }
                            break;
                        }
                    }
                    _reader = new BinaryReader(_ms);
                }
            }
            else if (Path.GetExtension(filename) == ".gb")
            {
                _reader = new BinaryReader(File.Open(filename, FileMode.Open));
            }          
            else
            {
                return false;
            }
            _reader.BaseStream.Seek(0x147, SeekOrigin.Begin);
            cartType = _reader.ReadByte();
            romSize = _reader.ReadByte();
            ramSize = _reader.ReadByte();
            _reader.BaseStream.Seek(0, SeekOrigin.Begin);
            switch (cartType)
            {
                case 0x00: break; // Rom only 
                case 0x01: cartMbc1 = true; break;
                case 0x02: cartMbc1 = true; cartRam = true; break;
                case 0x03: cartMbc1 = true; cartRam = true; cartBattery = true; break;
                case 0x05: cartMbc2 = true; break;
                case 0x06: cartMbc2 = true; cartBattery = true; break;
                case 0x08: cartRam = true; break;
                case 0x09: cartRam = true; cartBattery = true; break;


                default: Console.WriteLine("Unsupported Cartridge Type: " + cartType); _reader.Close(); return false;
            }



            prgRomSize = (0x8000) << romSize;
            prgROM = new byte[prgRomSize];
            if(ramSize != 0 )saveRAM = new byte[SaveSize[ramSize - 1]];
            for (int i = 0; i < prgRomSize; i++)
            {
                prgROM[i] = _reader.ReadByte();

            }
            _reader.Close();
            if (cartBattery)
            {
                saveFilename = Path.ChangeExtension(filename, ".sav");
                if(File.Exists(saveFilename))
                {
                    try
                    {
                        _reader = new BinaryReader(File.Open(saveFilename, FileMode.Open));
                        for (int i = 0; i < saveRAM.Length; i++)
                        {
                            saveRAM[i] = _reader.ReadByte();
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Coudlnt Read Save Data");
                    }
                }
                else
                {
                    WriteSaveFile();
                }
        
            }
            for (int i = 0x134; i < 0x142; i++)
            {
                if(prgROM[i] != 0)
                {
                    currentRomName += Convert.ToChar(prgROM[i]);
                }
                
            }

            romOffset = 0x4000;

            _reader.Close();
            if (_ms != null)
            { _ms.Close(); }
            return true;

        }

        public void WriteSaveFile()
        {
            if (!cartBattery) return;

            BinaryWriter _writer = new BinaryWriter(File.Open(saveFilename, FileMode.Create));
            for(int i = 0; i < saveRAM.Length - 1; i++)
            {
                _writer.Write(saveRAM[i]);
            }
            _writer.Close();
        }
        public byte ReadCart(ushort address)
        {
            if (address < 0x4000)
            {
                return prgROM[address];
            }
            else
            {
                return prgROM[(address & 0x3FFF) + romOffset];
            }
        }
        public void WriteCart(ushort address, byte data)
        {
            if (address < 0x2000)
            {
                if (cartRam)
                    enableCartRAM = (data & 0xF) == 0xA ? true : false;
            }
            else if (address < 0x4000)
            {
                if (cartMbc1)
                {
                    data &= 0x1F;
                    if (data == 0) { data = 1; }
                    bankNo = data;
                    romOffset = bankNo * 0x4000;
                    romOffset &= prgROM.Length - 1;
                }
                if(cartMbc2)
                {
                    data &= 0xF;
                    if (data == 0) { data = 1; }
                    bankNo = data;
                    romOffset = bankNo * 0x4000;
                    romOffset &= prgROM.Length - 1;
                }
            }
            else if (address < 0x6000)
            {
                if (cartMbc1)
                {
                    if (ramMode)
                    {
                        ramOffset = (data & 0x3) * 0x2000;
                        bankNo = (byte)(bankNo & 0x1F);
                    }
                    else if (romSize > 0x04)
                    {
                        bankNo |= (byte)(((data & 0x3) << 5));
                        romOffset = bankNo * 0x4000;
                        romOffset &= prgROM.Length - 1;
                        ramOffset = 0;
                    }
                }
            }
            else if (address < 0x8000)
            {
                if (cartMbc1)
                    ramMode = data != 0 ? true : false;
            }

        }

        public byte ReadCartRam(ushort address)
        {
            if (enableCartRAM)
            {
                if (cartBattery && cartRam)
                {
                    //if (address < 0xA1FF && _romoffset == 0)
                    //{
                    //    return _battery[address & 0x1ff];
                    //}
                    //else
                    //{
                    //    return _saveram[address & 0x1FFF + _ramoffset];
                    //}
                    return saveRAM[address & 0x1FFF + ramOffset];
                }
                else if (cartRam)
                {
                    return saveRAM[address & 0x1FFF + ramOffset];
                }
                else if (cartBattery)
                {
                    return batteryRAM[address & 0x1ff];
                }
            }
            return 0;
        }
        public void WriteCartRam(ushort address, byte data)
        {
            if (enableCartRAM)
            {
                if (cartBattery && cartRam)
                {
                    //if (address < 0xA1FF && _romoffset == 0)
                    //{
                    //    _battery[address & 0x1ff] = data;
                    //}
                    //else
                    //{
                    //    _saveram[address & 0x1FFF + _ramoffset] = data; 
                    //}
                    saveRAM[address & 0x1FFF + ramOffset] = data;
                }
                else if (cartRam)
                {
                    saveRAM[address & 0x1FFF + ramOffset] = data;
                }
                else if (cartBattery)
                {
                    batteryRAM[address & 0x1ff] = data;
                }
            }
        }
    }
}
