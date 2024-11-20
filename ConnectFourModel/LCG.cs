using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    internal class LCG
    {
        uint seed;
        uint a = 1664525;
        uint c = 1013904223;

        public LCG(uint seed)
        {
            this.seed = seed;
        }

        public int NextInt()
        {
            seed = seed * a + c;
            return (int)(seed & 0x7FFFFFFF);
        }

        public double NextDouble()
        {
            return (double)NextInt() / (double)int.MaxValue;
        }


    }
}
