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
        private int cycles = 0;
        private bool Envelope_enabled = false;

        private int[] DivisiorCodes = new int[]
        {
            8, 16, 32, 48, 64, 80, 96, 112
        };

        public NoiseChannel() { }


        private Random _rand = new Random();

        public void LengthTick()
        {
            if (LengthLoad != 0 && LengthEnable)
            {
                LengthLoad--;
                if(LengthLoad == 0)
                {
                     ChannelEnable = false;
                }
            }
        }

        public void EnvelopeTick()
        {
            if (EnvelopePeriod == 0) return;
            if (Envelope_enabled && --EnvelopePeriod == 0)
            {
                EnvelopePeriod = (byte)(Reg2 & 0x7);
                int new_vol = (byte)(EnvelopeAddMode ? StartingVolume + 1 : StartingVolume - 1);
                if (new_vol >= 0 && new_vol <= 15)
                {
                    StartingVolume = (byte)new_vol;
                }
                else
                {
                    Envelope_enabled = false;
                }

            }

        }

        public void Tick()
        {
            cycles++;
            if(ClockShift == 14 || ClockShift == 15)
            {
                return;
            }
      
            if (Frequency <= cycles)
            {
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
                Frequency = (DivisiorCodes[DivisorCode] << ClockShift) / 2;
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

            ChannelEnable = true;
            if (LengthLoad == 0) LengthLoad = 63;
            EnvelopePeriod = (byte)(Reg2 & 0x7);
            Envelope_enabled = true;
            StartingVolume = (byte)(Reg2 >> 4);
            Frequency = (DivisiorCodes[DivisorCode] << ClockShift) / 2;
            LFSR = 0x7FFF;
            if (StartingVolume == 0) ChannelEnable = false;
        }

        public void WriteSaveState(ref Savestate state, int channel)
        {
            state.SoundChannels[channel].Reg0 = Reg0;
            state.SoundChannels[channel].Reg1 = Reg1;
            state.SoundChannels[channel].Reg2 = Reg2;
            state.SoundChannels[channel].Reg3 = Reg3;
            state.SoundChannels[channel].Reg4 = Reg4;
            state.SoundChannels[channel].ChannelEnable = ChannelEnable;
            state.SoundChannels[channel].LengthLoad = LengthLoad;
            state.SoundChannels[channel].StartingVolume = StartingVolume;
            state.SoundChannels[channel].EnvelopeAddMode = EnvelopeAddMode;
            state.SoundChannels[channel].EnvelopePeriod = EnvelopePeriod;
            state.SoundChannels[channel].LengthEnable = LengthEnable;
            state.SoundChannels[channel].Sample = Sample;
            state.SoundChannels[channel].Frequency = Frequency;
            state.SoundChannels[channel].Envelope_enabled = Envelope_enabled;
            state.SoundChannels[channel].WidthOfLFSR = WidthOfLFSR;
            state.SoundChannels[channel].DivisorCode = DivisorCode;
            state.SoundChannels[channel].LFSR = LFSR;
        }
        public void LoadSaveState(Savestate state, int channel)
        {
            Reg0 = state.SoundChannels[channel].Reg0;
            Reg1 = state.SoundChannels[channel].Reg1;
            Reg2 = state.SoundChannels[channel].Reg2;
            Reg3 = state.SoundChannels[channel].Reg3;
            Reg4 = state.SoundChannels[channel].Reg4;
            ChannelEnable = state.SoundChannels[channel].ChannelEnable;
            LengthLoad = state.SoundChannels[channel].LengthLoad;
            StartingVolume = state.SoundChannels[channel].StartingVolume;
            EnvelopeAddMode = state.SoundChannels[channel].EnvelopeAddMode;
            EnvelopePeriod = state.SoundChannels[channel].EnvelopePeriod;
            LengthEnable = state.SoundChannels[channel].LengthEnable;
            Sample = state.SoundChannels[channel].Sample;
            Frequency = state.SoundChannels[channel].Frequency;
            Envelope_enabled = state.SoundChannels[channel].Envelope_enabled;
            WidthOfLFSR = state.SoundChannels[channel].WidthOfLFSR;
            DivisorCode = state.SoundChannels[channel].DivisorCode;
            LFSR = state.SoundChannels[channel].LFSR;
        }
    }

}
