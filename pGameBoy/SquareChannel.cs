using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    class SquareChannel
    {
        /*       Square 1
        NR10 FF10 -PPP NSSS Sweep period, negate, shift
        NR11 FF11 DDLL LLLL Duty, Length load (64-L)
        NR12 FF12 VVVV APPP Starting volume, Envelope add mode, period
        NR13 FF13 FFFF FFFF Frequency LSB
        NR14 FF14 TL-- -FFF Trigger, Length enable, Frequency MSB

                Square 2
        FF15 ---- ---- Not used
        NR21 FF16 DDLL LLLL Duty, Length load (64-L)
        NR22 FF17 VVVV APPP Starting volume, Envelope add mode, period
        NR23 FF18 FFFF FFFF Frequency LSB
        NR24 FF19 TL-- -FFF Trigger, Length enable, Frequency MSB*/

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

        private bool SweepEnabled = false;
        private int ShadowFreq = 0;
        private int SweepStep = 0;
        private int Cycles = 0;
        private bool Envelope_enabled;

  


        public SquareChannel()
        {

        }

        private byte[] DutyCycles = new byte[4]
        {
                0x1,    //00000001  12.5%
                0x81,   //10000001  25%
                0x87,   //10000111  50%
                0x7E    //01111110  75%
        };

        public void Tick()
        {
            Cycles++;
            if (Frequency <= Cycles)
            {
                
                if (CurrentDuty < 8)
                {
                    Sample = ((DutyCycles[Duty] >> CurrentDuty) & 0x1) != 0 ? StartingVolume : 0;
                    Cycles = 0;
                }
                else
                {
                    int value = (Reg4 << 8) | Reg3;
                    value &= 0x7FF;
                    Frequency = (2048 - value) * 2;
                    Cycles = 0;
                    CurrentDuty = 0;
                    Sample = ((DutyCycles[Duty] >> CurrentDuty) & 0x1) != 0 ? StartingVolume : 0;
                }
                if (!ChannelEnable) Sample = 0;
                CurrentDuty++;

            }



        }

        public void LengthTick()
        {
            if (LengthLoad != 0 && LengthEnable)
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

            if (EnvelopePeriod == 0) return;
            if (Envelope_enabled && --EnvelopePeriod == 0)
            {
                EnvelopePeriod = (byte)(Reg2 & 0x7);
                int new_vol = (byte)(EnvelopeAddMode ? StartingVolume + 1 : StartingVolume - 1);
                if(new_vol >= 0 && new_vol <=15)
                {
                    StartingVolume = (byte)new_vol;
                }
                else
                {
                    Envelope_enabled = false;
                }
                
            }

        }

        private void CalculateSweep()
        {
            int newFreq = ShadowFreq >> Shift;
            ShadowFreq = Negate ? ShadowFreq - newFreq : ShadowFreq + newFreq;
            if (ShadowFreq > 2047)
            {
                ChannelEnable = false;
                return;
            }
            Reg3 = (byte)(ShadowFreq & 0xFF);
            Reg4 &= 0xF8;
            Reg4 |= (byte)((ShadowFreq >> 8) & 0x7);
            Frequency = (2048 - ShadowFreq) * 2;
        }

        public void SweepTick()
        {
            
            if (SweepEnabled && SweepPeriod != 0)
            {
                SweepStep++;
                if (SweepStep >= SweepPeriod)
                {
                    CalculateSweep();
                    SweepStep = 0;
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

            /*       Square 1
            NR10 FF10 -PPP NSSS Sweep period, negate, shift
            NR11 FF11 DDLL LLLL Duty, Length load (64-L)
            NR12 FF12 VVVV APPP Starting volume, Envelope add mode, period
            NR13 FF13 FFFF FFFF Frequency LSB
            NR14 FF14 TL-- -FFF Trigger, Length enable, Frequency MSB*/

            ChannelEnable = true;
            if (LengthLoad == 0) LengthLoad = 63;
            int value = (Reg4 << 8) | Reg3;
            value &= 0x7FF;
            ShadowFreq = value;
            Frequency = (2048 - value) * 2;
            EnvelopePeriod = (byte)(Reg2 & 0x7);
            Envelope_enabled = true;
            StartingVolume = (byte)(Reg2 >> 4);
            if (StartingVolume == 0) ChannelEnable = false;


        }

        public void TriggerCh1()
        {
            SweepPeriod = (byte)((Reg0 >> 4) & 0x7);
            SweepEnabled = false;
            if (SweepPeriod != 0) SweepEnabled = true; 
            if(Shift != 0)
            {
                SweepEnabled = true;
                CalculateSweep();
            }
        }

        public void WriteSaveState(ref Savestate state, int channel)
        {
            state.SoundChannels[channel].Reg0 = Reg0;
            state.SoundChannels[channel].Reg1 = Reg1;
            state.SoundChannels[channel].Reg2 = Reg2;
            state.SoundChannels[channel].Reg3 = Reg3;
            state.SoundChannels[channel].Reg4 = Reg4;
            state.SoundChannels[channel].ChannelEnable = ChannelEnable;
            state.SoundChannels[channel].SweepPeriod = SweepPeriod;
            state.SoundChannels[channel].Negate = Negate;
            state.SoundChannels[channel].Shift = Shift;
            state.SoundChannels[channel].Duty = Duty;
            state.SoundChannels[channel].LengthLoad = LengthLoad;
            state.SoundChannels[channel].StartingVolume = StartingVolume;
            state.SoundChannels[channel].EnvelopeAddMode = EnvelopeAddMode;
            state.SoundChannels[channel].EnvelopePeriod = EnvelopePeriod;
            state.SoundChannels[channel].LengthEnable = LengthEnable;
            state.SoundChannels[channel].CurrentDuty = CurrentDuty;
            state.SoundChannels[channel].Sample = Sample;
            state.SoundChannels[channel].Frequency = Frequency;
            state.SoundChannels[channel].SweepEnabled = SweepEnabled;
            state.SoundChannels[channel].ShadowFreq = ShadowFreq;
            state.SoundChannels[channel].SweepStep = SweepStep;
            state.SoundChannels[channel].Cycles = Cycles;
            state.SoundChannels[channel].Envelope_enabled = Envelope_enabled;
        }

        public void LoadSaveState(Savestate state, int channel)
        {
            Reg0 = state.SoundChannels[channel].Reg0;
            Reg1 = state.SoundChannels[channel].Reg1;
            Reg2 = state.SoundChannels[channel].Reg2;
            Reg3 = state.SoundChannels[channel].Reg3;
            Reg4 = state.SoundChannels[channel].Reg4;
            ChannelEnable = state.SoundChannels[channel].ChannelEnable;
            SweepPeriod = state.SoundChannels[channel].SweepPeriod;
            Negate = state.SoundChannels[channel].Negate;
            Shift = state.SoundChannels[channel].Shift;
            Duty = state.SoundChannels[channel].Duty;
            LengthLoad = state.SoundChannels[channel].LengthLoad;
            StartingVolume = state.SoundChannels[channel].StartingVolume;
            EnvelopeAddMode = state.SoundChannels[channel].EnvelopeAddMode;
            EnvelopePeriod = state.SoundChannels[channel].EnvelopePeriod;
            LengthEnable = state.SoundChannels[channel].LengthEnable;
            CurrentDuty = state.SoundChannels[channel].CurrentDuty;
            Sample = state.SoundChannels[channel].Sample;
            Frequency = state.SoundChannels[channel].Frequency;
            SweepEnabled = state.SoundChannels[channel].SweepEnabled;
            ShadowFreq = state.SoundChannels[channel].ShadowFreq;
            SweepStep = state.SoundChannels[channel].SweepStep;
            Cycles = state.SoundChannels[channel].Cycles;
            Envelope_enabled = state.SoundChannels[channel].Envelope_enabled;
        }




    }
}
