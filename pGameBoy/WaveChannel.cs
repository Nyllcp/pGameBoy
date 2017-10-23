using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class WaveChannel
    {
        /*        Wave
        NR30 FF1A E--- ---- DAC power
        NR31 FF1B LLLL LLLL Length load (256-L)
        NR32 FF1C -VV- ---- Volume code (00=0%, 01=100%, 10=50%, 11=25%)
        NR33 FF1D FFFF FFFF Frequency LSB
        NR34 FF1E TL-- -FFF Trigger, Length enable, Frequency MSB */
        public byte Reg0 = 0;
        public byte Reg1 = 0;
        public byte Reg2 = 0;
        public byte Reg3 = 0;
        public byte Reg4 = 0;

        public bool ChannelEnable = false;
        public bool DacPower = false;
        public byte LengthLoad = 0;
        public byte Volume = 0;
        public int Frequency = 0;
        public bool LengthEnable = false;

        public byte[] WaveTable = new byte[0X10];

        private int cycles = 0;
        private int wavePos = 0;

        public int Sample = 0;

        private int[] VolumeShift = new int[] { 4, 0, 1, 2 };
        public WaveChannel()
        {

        }

        public void Tick()
        {
            cycles++;
            if (Frequency <= cycles)
            {
                int value = (Reg4 << 8) | Reg3;
                value &= 0x7FF;
                Frequency = (2048 - value);
                wavePos++;
                wavePos = wavePos % 32;

                if (wavePos % 2 == 0)
                {
                    Sample = (WaveTable[wavePos >> 1] >> 4);
                }
                else
                {
                    Sample = (WaveTable[(wavePos - 1) >> 1] & 0xF);
                }
                
                if (!ChannelEnable || !DacPower) Sample = 0;
                Sample = Sample >> VolumeShift[Volume];
                cycles = 0;
            }



        }
        public void LengthTick()
        {
            if (LengthLoad == 0)
            {
                ChannelEnable = false;
                return;
            }
            if (LengthEnable)
            {
                LengthLoad--;
                if (LengthLoad == 0)
                {
                    ChannelEnable = false;
                }


            }
        }

        public void Trigger()
        {
            /*  Writing a value to NRx4 with bit 7 set causes the following things to occur:
                Channel is enabled (see length counter).
                If length counter is zero, it is set to 64 (256 for wave channel).
                Frequency timer is reloaded with period.
                Volume envelope timer is reloaded with period.
                Channel volume is reloaded from NRx2.
                Noise channel's LFSR bits are all set to 1.
                Wave channel's position is set to 0 but sample buffer is NOT refilled.
                Square 1's sweep does several things (see frequency sweep).
                Note that if the channel's DAC is off, after the above actions occur the channel will be immediately disabled again.*/
            ChannelEnable = true;
            //cycles = 0;
            if (LengthLoad == 0) LengthLoad = 255;
            int value = (Reg4 << 8) | Reg3;
            value &= 0x7FF;
            Frequency = (2048 - value);
            wavePos = 0;
            Sample = 0;
            if (!DacPower) ChannelEnable = false;

        }

    }
}
