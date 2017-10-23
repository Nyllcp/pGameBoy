using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using NAudio.Wave;

namespace pGameBoy
{
    class Audio
    {


        const int SAMPLE_RATE = 44100;

        private BufferedWaveProvider _soundbuffer;
        private DirectSoundOut _output;
        

        public Audio()
        {
            _output = new DirectSoundOut();
            _soundbuffer = new BufferedWaveProvider(new WaveFormat(SAMPLE_RATE,1));
            _soundbuffer.BufferLength = ((SAMPLE_RATE / 60) * 20); //Aproxx 20 frames of sound in buffer
            _soundbuffer.DiscardOnBufferOverflow = true;
            _output.Init(_soundbuffer);
        }
        ~Audio()
        {
            _output.Dispose();
        }
        public void AddSample(byte[] buffer, int numberofsamples, bool startSound)
        {
            _soundbuffer.AddSamples(buffer, 0, numberofsamples * 2);
            if(startSound)
            {
                _output.Play();
            }
        }
        public bool IsPlaying()
        {
            return _output.PlaybackState == PlaybackState.Playing;
        }
        public int GetBufferedBytes()
        {
            return _soundbuffer.BufferedBytes;
        }
        public void Play()
        {
            _output.Play();
        }



    }
}
