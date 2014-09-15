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
        public const int MOUSE_UP_CODE = -8472;

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

        public static DateTime ConvertFromUnixTime(string time)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(double.Parse(time.Replace(".0", ""))).ToLocalTime();
        }
    }
}
