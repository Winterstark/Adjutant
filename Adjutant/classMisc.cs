using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adjutant
{
    //global enums
    public enum MailCheckAction { MailInit, TimerCheck, ForceOutput, NoAction };

    class Misc
    {
        public static int GetNextDelimiter(string txt, int lb, char delimiter1, char delimiter2)
        {
            int delimiterInd1 = txt.IndexOf(delimiter1, lb);
            int delimiterInd2 = txt.IndexOf(delimiter2, lb);

            if (delimiterInd1 == -1)
                return delimiterInd2;
            else if (delimiterInd2 == -1)
                return delimiterInd1;
            else
                return Math.Min(delimiterInd1, delimiterInd2);
        }
    }
}
