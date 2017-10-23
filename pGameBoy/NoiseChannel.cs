using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class NoiseChannel
    {
        /*       Noise
        FF1F ---- ---- Not used
        NR41 FF20 --LL LLLL Length load (64-L)
        NR42 FF21 VVVV APPP Starting volume, Envelope add mode, period
        NR43 FF22 SSSS WDDD Clock shift, Width mode of LFSR, Divisor code
        NR44 FF23 TL-- ---- Trigger, Length enable*/
        public byte Reg0 = 0;
        public byte Reg1 = 0;
        public byte Reg2 = 0;
        public byte Reg3 = 0;
        public byte Reg4 = 0;

        public byte LengthLoad = 0;
        public byte StartingVolume = 0;
        public bool EnvelopeAddMode = false;
        public byte EnvelopePeriod = 0;
        public byte ClockShift = 0;
        public bool WidthOfLFSR = false;
        public byte DivisorCode = 0;
        public bool LengthEnable = false;
        public bool ChannelEnable = false;
        public int Frequency = 0;
        public int Sample = 0;

        private int LFSR = 0;

        private int EnvelopeStep = 0;
        private int cycles = 0;

        private int[] DivisiorCodes = new int[]
        {
            8, 16, 32, 48, 64, 80, 96, 112
        };

        public NoiseChannel() { }

        private Random _rand = new Random();

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

        public void EnvelopeTick()
        {
            if (EnvelopePeriod == 0 || StartingVolume == 0 && EnvelopeAddMode == false || StartingVolume == 15 && EnvelopeAddMode == true) return;
            EnvelopeStep++;
            if (EnvelopeStep >= EnvelopePeriod)
            {
                StartingVolume = (byte)(EnvelopeAddMode == true ? StartingVolume + 1 : StartingVolume - 1);
                EnvelopeStep = 0;  
            }

        }

        public void Tick()
        {
            cycles++;
            if (Frequency <= cycles)
            {
                Frequency = (DivisiorCodes[DivisorCode] << ClockShift) / 2;
                int bit0 = LFSR & 1;
                int bit1 = (LFSR >> 1) & 1;

                LFSR = LFSR >> 1;
                LFSR |= ((bit0 ^ bit1) << 14);
                LFSR &= 0x7FFF;
                if (WidthOfLFSR == true)
                {
                    LFSR &= ~(1 << 6);
                    LFSR |= (bit0 ^ bit1) << 6;
                }

                Sample = (LFSR & 1) != 0 ? 0 : StartingVolume;
                if (!ChannelEnable) Sample = 0;
                cycles = 0;
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
            /*        Noise
                FF1F ---- ---- Not used
                NR41 FF20 --LL LLLL Length load (64-L)
                NR42 FF21 VVVV APPP Starting volume, Envelope add mode, period
                NR43 FF22 SSSS WDDD Clock shift, Width mode of LFSR, Divisor code
                NR44 FF23 TL-- ---- Trigger, Length enable*/

            cycles = 0;
            ChannelEnable = true;
            if (LengthLoad == 0) LengthLoad = 64;
            EnvelopePeriod = (byte)(Reg2 & 0x7);
            EnvelopeAddMode = ((Reg2 >> 3) & 1) != 0 ? true : false;
            EnvelopeStep = 0;
            StartingVolume = (byte)(Reg2 >> 4);
            Frequency = (DivisiorCodes[DivisorCode] << ClockShift) / 2;
            LFSR = 0x7FFF;
        }
    }

}
