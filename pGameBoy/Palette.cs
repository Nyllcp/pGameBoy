using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pGameBoy
{
    public static class Palette
    {
        public static uint[] BlackAndWhite = new uint[]
        {
            0xFFFFFFFF,
            0xFFC0C0C0,
            0xFF606060,
            0xFF000000
        };

        public static uint[] BGB = new uint[]
    {
            0xFFD0F8E0,
            0xFF70C088,
            0xFF566834,
            0xFF201808
    };

        public static uint[] RealDMG = new uint[]
        {
            0xFF0F867F,
            0xFF457C57,
            0xFF485D36,
            0xFF3B452A,
        };

        public static uint[] Zelda = new uint[]
        {
            0xFFB0D8F8,
            0xFF78C078,
            0xFF408868,
            0xFF203858,
        };
        public static uint[] Kirby = new uint[]
        {
            0xFFF8C0F8,
            0xFF8888E8,
            0xFFE83078,
            0xFF982828,
        };
        public static uint[] SML2 = new uint[]
       {
            0xFFB8F8F0,
            0xFF78A8E0,
            0xFF00C808,
            0xFF000000,
       };
    }
}
