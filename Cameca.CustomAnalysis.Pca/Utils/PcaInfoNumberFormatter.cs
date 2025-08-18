using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cameca.CustomAnalysis.Pca.Utils
{

    public class PcaInfoNumberFormatter
    {
        public PcaInfoNumberFormatter()
        {
        }

        // the input should be a string like "17.435" or 198.00"
        // we would like the trailing zeros in "198.00" to go away
        private string RemoveTrailingZeros(string val)
        {
            val.TrimEnd('0');

            val.TrimEnd('.');
            return val;
        }

        public string StringForGridInfoTable(float val)
        {
            if (val == 0.0)
            {
                return "0";
            }
            if (val > 1000)
            {
                return val.ToString("F0");
            }
            if (val > 100)
            {
                return RemoveTrailingZeros(val.ToString("F1"));
            }
            if (val > 10)
            {
                return RemoveTrailingZeros(val.ToString("F2"));
            }
            if (val > 1)
            {
                return RemoveTrailingZeros(val.ToString("F3"));
            }
             return RemoveTrailingZeros(val.ToString("F4"));
        }
    }
}
