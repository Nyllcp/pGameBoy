using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace pGameBoy
{
    class Apu
    {
        private WaveChannel _waveChannel;
        private SquareChannel _squareChannel1;
        private SquareChannel _squareChannel2;
        private NoiseChannel _noiseChannel;

        const int SAMPLE_RATE = 44100;
        const int DMG_CPUFREQ = 4194304;
        const int CGB_CPUFREQ = 8388608;

        private int soundCycles = 0;
        private int frameSequenceStep = 0;


        public byte[] Samples = new byte[2048]; // Abit more then a frame worth of samples. 44100 / 60 = 735. 2 bytes per sample 1470 bytes..
        public int NumberOfSamples = 0;
        private byte channelCtrlReg = 0;
        private byte soundOutputReg = 0;
        private bool soundEnabled = true;

        private double capacitor = 0.0;

        public Apu()
        {
            _waveChannel = new WaveChannel();
            _squareChannel1 = new SquareChannel();
            _squareChannel2 = new SquareChannel();
            _noiseChannel = new NoiseChannel();

        }

        private void Sample(bool sndEnabled)
        {
            
            if (NumberOfSamples * 2 > Samples.Length - 2) return;
            int tempsample = 0;
            if((soundOutputReg & 1) != 0 || ((soundOutputReg >> 4) & 1) != 0)
            {
                tempsample += _squareChannel1.Sample;
            }
            if (((soundOutputReg >> 1)& 1) != 0 || ((soundOutputReg >> 5) & 1) != 0)
            {
                tempsample += _squareChannel2.Sample;
            }
            if (((soundOutputReg >> 2) & 1) != 0 || ((soundOutputReg >> 6) & 1) != 0)
            {
                tempsample += _waveChannel.Sample;
            }
            if (((soundOutputReg >> 3) & 1) != 0 || ((soundOutputReg >> 7) & 1) != 0)
            {
                tempsample += _noiseChannel.Sample;
            }
            int sample = HighPass(tempsample << 8, sndEnabled);
            Samples[NumberOfSamples * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            Samples[NumberOfSamples * 2] = (byte)(sample & 0xFF);
            NumberOfSamples++;
        }

        private int HighPass(int sample, bool sndEnabled)
        {
            double value = 0.0;
            if (sndEnabled)
            {
                value = sample - capacitor;
                /* Capacitor slowly charges to 'in' via their difference
                The charge factor can be calculated for any output sampling rate as 0.999958^(4194304/rate). 
                So if you were applying high_pass() at 44100 Hz, you'd use a charge factor of 0.996.
                use 0.998943 for MGB&CGB */
                capacitor = sample - value * 0.996;
            }
                 
            return (int)value;
        }

        public void ApuTick()
        {
            soundCycles++;
            if (soundCycles % (DMG_CPUFREQ / SAMPLE_RATE) == 0)
            {
                Sample(soundEnabled);
            }

            if (!soundEnabled) return;
            
            if (soundCycles % 2 == 0)
            {
                _waveChannel.Tick();
                _squareChannel1.Tick();
                _squareChannel2.Tick();
                _noiseChannel.Tick();

            }
           
            if(soundCycles % (DMG_CPUFREQ / 512) == 0)
            {
                FrameSequencer();
            }
            
        }

        public void FrameSequencer()
        {
            if (frameSequenceStep % 2 == 0)
            {
                //256 hz lenght counter timer
                //Shortest timer, get sound buffer
                _waveChannel.LengthTick();
                _squareChannel1.LengthTick();
                _squareChannel2.LengthTick();
                _noiseChannel.LengthTick();

            }
            if (frameSequenceStep == 2 || frameSequenceStep == 6)
            {
                // 128 hz Sweep timer:
                _squareChannel1.SweepTick();
            }
            if (frameSequenceStep == 7)
            {
                // 64 hz timer, envelope
                _squareChannel1.EnvelopeTick();
                _squareChannel2.EnvelopeTick();
                _noiseChannel.EnvelopeTick();
            }
            frameSequenceStep++;
            if (frameSequenceStep > 7)
            {
                frameSequenceStep = 0;
            }

        }

        public void WriteSoundRegister(ushort address, byte data)
        {
            /*    Wave
            NR30 FF1A E--- ---- DAC power
            NR31 FF1B LLLL LLLL Length load (256-L)
            NR32 FF1C -VV- ---- Volume code (00=0%, 01=100%, 10=50%, 11=25%)
            NR33 FF1D FFFF FFFF Frequency LSB
            NR34 FF1E TL-- -FFF Trigger, Length enable, Frequency MSB
            /*       Square 1
            NR10 FF10 -PPP NSSS Sweep period, negate, shift
            NR11 FF11 DDLL LLLL Duty, Length load (64-L)
            NR12 FF12 VVVV APPP Starting volume, Envelope add mode, period
            NR13 FF13 FFFF FFFF Frequency LSB
            NR14 FF14 TL-- -FFF Trigger, Length enable, Frequency MSB
            Square 2
            FF15-------- Not used
            NR21 FF16 DDLL LLLL Duty, Length load (64 - L)
            NR22 FF17 VVVV APPP Starting volume, Envelope add mode, period
            NR23 FF18 FFFF FFFF Frequency LSB
            NR24 FF19 TL-- - FFF Trigger, Length enable, Frequency MSB 
                   Noise
            FF1F ---- ---- Not used
            NR41 FF20 --LL LLLL Length load (64-L)
            NR42 FF21 VVVV APPP Starting volume, Envelope add mode, period
            NR43 FF22 SSSS WDDD Clock shift, Width mode of LFSR, Divisor code
            NR44 FF23 TL-- ---- Trigger, Length enable*/
                switch (address & 0xff)
            {
                //Square 1
                case 0x10:
                    _squareChannel1.SweepPeriod = (byte)((data >> 4) & 0x7);
                    _squareChannel1.Negate = ((data >> 3) & 1) != 0 ? true : false;
                    _squareChannel1.Shift = (byte)(data & 0x7);
                    _squareChannel1.Reg0 = data;
                    break; 
                case 0x11:
                    _squareChannel1.Duty = (byte)(data >> 6);
                    _squareChannel1.LengthLoad = (byte)(0x3F - (data & 0x3F));
                    _squareChannel1.Reg1 = data;
                    break;
                case 0x12:
                    _squareChannel1.StartingVolume = (byte)(data >> 4);
                    _squareChannel1.EnvelopeAddMode = ((data >> 3) & 1) != 0 ? true : false;
                    _squareChannel1.EnvelopePeriod = (byte)(data & 0x7);
                    _squareChannel1.Reg2 = data;
                    if (_squareChannel1.StartingVolume == 0 && !_squareChannel1.EnvelopeAddMode) _squareChannel1.ChannelEnable = false;
                    break;
                case 0x13:
                    _squareChannel1.Reg3 = data;
                    break;
                case 0x14:
                    _squareChannel1.Reg4 = data;
                    _squareChannel1.LengthEnable = ((data >> 6) & 1) != 0 ? true : false;
                    if ((data >> 7) != 0)
                    {
                        _squareChannel1.Trigger();
                        _squareChannel1.TriggerCh1();
                    }
                    break;
                //Square 2
                case 0x15: break; //not used
                case 0x16:
                    _squareChannel2.Duty = (byte)(data >> 6);
                    _squareChannel2.LengthLoad = (byte)(0x3F - (data & 0x3F));
                    _squareChannel2.Reg1 = data;
                    break;
                case 0x17:
                    _squareChannel2.StartingVolume = (byte)(data >> 4);
                    _squareChannel2.EnvelopeAddMode = ((data >> 3) & 1) != 0 ? true : false;
                    _squareChannel2.EnvelopePeriod = (byte)(data & 0x7);
                    _squareChannel2.Reg2 = data;
                    if (_squareChannel2.StartingVolume == 0 && !_squareChannel2.EnvelopeAddMode) _squareChannel2.ChannelEnable = false;
                    break;
                case 0x18:
                    _squareChannel2.Reg3 = data;
                    break;
                case 0x19:
                    _squareChannel2.Reg4 = data;
                    _squareChannel2.LengthEnable = ((data >> 6) & 1) != 0 ? true : false;
                    if ((data >> 7) != 0) _squareChannel2.Trigger();
                    break;
                //Wave
                case 0x1A:
                    _waveChannel.DacPower = data >> 7 != 0 ? true : false;
                    if (!_waveChannel.DacPower) _waveChannel.ChannelEnable = false;
                    _waveChannel.Reg0 = data;
                    break;
                case 0x1B:
                    _waveChannel.LengthLoad = (byte)(0xFF - data);
                    _waveChannel.Reg1 = data;
                    break;
                case 0x1C:
                    _waveChannel.Volume = (byte)((data >> 5) & 0x3);
                    _waveChannel.Reg2 = data;
                    break;
                case 0x1D: _waveChannel.Reg3 = data; break;
                case 0x1E:
                    {
                        _waveChannel.LengthEnable = ((data >> 6) & 1) != 0 ? true : false;
                        _waveChannel.Reg4 = data;
                        if ((data >> 7) != 0) _waveChannel.Trigger();
                        break;
                    }
                //Noise
                case 0x1F: break; //not used
                case 0x20:
                    _noiseChannel.LengthLoad = (byte)(0x3F - (data & 0x3F));
                    _noiseChannel.Reg1 = data;
                    break; 
                case 0x21:
                    _noiseChannel.StartingVolume = (byte)(data >> 4);
                    _noiseChannel.EnvelopeAddMode = ((data >> 3) & 1) != 0 ? true : false;
                    _noiseChannel.EnvelopePeriod = (byte)(data & 0x7);
                    _noiseChannel.Reg2 = data;
                    if (_noiseChannel.StartingVolume == 0 && !_noiseChannel.EnvelopeAddMode) _noiseChannel.ChannelEnable = false;
                    break;
                case 0x22:
                    _noiseChannel.ClockShift = (byte)(data >> 4);
                    _noiseChannel.WidthOfLFSR = ((data >> 3) & 1) != 0 ? true : false;
                    _noiseChannel.DivisorCode = (byte)(data & 0x7);
                    _noiseChannel.Reg3 = data;
                    break;
                case 0x23:
                    _noiseChannel.LengthEnable = ((data >> 6) & 1) != 0 ? true : false;
                    _noiseChannel.Reg4 = data;
                    if ((data >> 7) != 0) _noiseChannel.Trigger();
                    break;

                //CTRL
                case 0x24:
                    channelCtrlReg = data; break;
                case 0x25:
                    soundOutputReg = data; break;
                case 0x26:
                        {
                        soundEnabled = ((data >> 7) & 0x1) != 0 ? true : false;
                        if (!soundEnabled) ResetApu();
                            break;
                        }
            }
            if((address & 0xFF) >= 0x30 && (address & 0xFF) <= 0x3F)
            {
                _waveChannel.WaveTable[address & 0xF] = data;
            }
        }
        public byte ReadSoundRegister(ushort address)
        {
            int value = 0;
            switch(address & 0xFF)
            {
                case 0x10:
                    value |= _squareChannel1.SweepPeriod << 4;
                    value |= _squareChannel1.Negate ? 1 << 3 : 0;
                    value |= _squareChannel1.Shift & 0x7;
                    break;
                case 0x11:
                    value |= _squareChannel1.Duty << 6;
                    value |= _squareChannel1.LengthLoad & 0x3F;  
                    break;
                case 0x12:
                    value |= _squareChannel1.StartingVolume << 4;
                    value |= _squareChannel1.EnvelopeAddMode ? 1 << 3 : 0;
                    value |= _squareChannel1.EnvelopePeriod & 0x7;
                    break;
                case 0x13:
                    value = _squareChannel1.Reg3;
                    break;
                case 0x14:
                    value = _squareChannel1.Reg4;
                    break;
                //Square 2
                case 0x15: break; //not used
                case 0x16:
                    value |= _squareChannel2.SweepPeriod << 4;
                    value |= _squareChannel2.Negate ? 1 << 3 : 0;
                    value |= _squareChannel2.Shift & 0x7;
                    break;
                case 0x17:
                    value |= _squareChannel2.Duty << 6;
                    value |= _squareChannel2.LengthLoad & 0x3F;
                    break;
                case 0x18:
                    value = _squareChannel2.Reg3;
                    break;
                case 0x19:
                    value = _squareChannel2.Reg4;
                    break;
                //Wave
                case 0x1A:
                    value = _waveChannel.DacPower ? 1 << 7 : 0;
                    break;
                case 0x1B:
                    value = _waveChannel.LengthLoad;
                    break;
                case 0x1C:
                    value = _waveChannel.Volume << 5;
                    break;
                case 0x1E:
                    value = _waveChannel.Reg4;
                    break;
                //Noise
                case 0x1F: break; //not used
                case 0x20:
                    value = _noiseChannel.LengthLoad;
                    break;
                case 0x21:
                    value |= _noiseChannel.StartingVolume << 4;
                    value |= _noiseChannel.EnvelopeAddMode ? 1 << 3 : 0;
                    value |= _noiseChannel.EnvelopePeriod;
                    break;
                case 0x22:
                    value |= _noiseChannel.ClockShift << 4;
                    value |= _noiseChannel.WidthOfLFSR ? 1 << 3 : 0;
                    value |= _noiseChannel.DivisorCode;
                    break;
                case 0x23:
                    value = _noiseChannel.Reg4;
                    break;
                case 0x24: value = channelCtrlReg; break;
                case 0x25: value = soundOutputReg; break;
                case 0x26:
                    value = _squareChannel1.ChannelEnable ? 1 : 0;
                    value |= _squareChannel2.ChannelEnable  ? 1 << 1 : 0;
                    value |= _waveChannel.ChannelEnable  ? 1 << 2 : 0;
                    value |= _noiseChannel.ChannelEnable ? 1 << 3: 0;
                    value |= soundEnabled ? 1 << 7 : 0;
                    value |= 0x70;
                    break;
            }
            return (byte)value;
        }

        public void ResetApu()
        {
            capacitor = 0.0;
            //all regs set to 0
            _squareChannel1.Reg0 = 0;
            _squareChannel1.Reg1 = 0;
            _squareChannel1.Reg2 = 0;
            _squareChannel1.Reg3 = 0;
            _squareChannel1.Reg4 = 0;
            _squareChannel2.Reg0 = 0;
            _squareChannel2.Reg1 = 0;
            _squareChannel2.Reg2 = 0;
            _squareChannel2.Reg3 = 0;
            _squareChannel2.Reg4 = 0;
            _waveChannel.Reg0 = 0;
            _waveChannel.Reg1 = 0;
            _waveChannel.Reg2 = 0;
            _waveChannel.Reg3 = 0;
            _waveChannel.Reg4 = 0;
            _noiseChannel.Reg0 = 0;
            _noiseChannel.Reg1 = 0;
            _noiseChannel.Reg2 = 0;
            _noiseChannel.Reg3 = 0;
            _noiseChannel.Reg4 = 0;

            _squareChannel1.Sample = 0;
            _squareChannel2.Sample = 0;
            _waveChannel.Sample = 0;
            _noiseChannel.Sample = 0;
        }

        public void WriteSaveState(ref Savestate state)
        {
            Array.Copy(Samples, state.Samples, Samples.Length);
            state.soundCycles = soundCycles;
            state.frameSequenceStep = frameSequenceStep;
            state.NumberOfSamples = NumberOfSamples;
            state.channelCtrlReg = channelCtrlReg;
            state.soundOutputReg = soundOutputReg;
            state.soundEnabled = soundEnabled;
            _squareChannel1.WriteSaveState(ref state, 0);
            _squareChannel2.WriteSaveState(ref state, 1);
            _waveChannel.WriteSaveState(ref state, 2);
            _noiseChannel.WriteSaveState(ref state, 3);
        }
        public void LoadSaveState(Savestate state)
        {
            Array.Copy(state.Samples, Samples, Samples.Length);
            soundCycles = state.soundCycles;
            frameSequenceStep = state.frameSequenceStep;
            NumberOfSamples = state.NumberOfSamples;
            channelCtrlReg = state.channelCtrlReg;
            soundOutputReg = state.soundOutputReg;
            soundEnabled = state.soundEnabled;
            _squareChannel1.LoadSaveState(state, 0);
            _squareChannel2.LoadSaveState(state, 1);
            _waveChannel.LoadSaveState(state, 2);
            _noiseChannel.LoadSaveState(state, 3);
        }


    }
}
