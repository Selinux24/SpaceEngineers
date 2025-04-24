using System.Linq;
using System;

namespace Test
{
    static class Program
    {
        public static void Main(string[] args)
        {
            /*

–––( BuildInfo )–––
(Text from 13s ago)

REQUEST_STATUS
 1 REQUEST_STATUS

ParseMessage: Command=REQUEST_STATUS|From=BaseBETA1
Mensaje recibido: Command=REQUEST_STATUS|From=BaseBETA1
REQUEST_STATUS
 1 REQUEST_STATUS

SendIGCMessage: Command=STATUS|To=BaseBETA1|From=NaveBETA1|Status=Idle|Origin=|OriginPosition=0:0:0|Destination=|DestinationPosition=0:0:0|Position=-50030.1467906485:-87218.8541785618:-43613.1279604134
––––––
            */

            string[] logLines =
            [
                "ParseMessage: Command=REQUEST_STATUS|From=BaseBETA1",
                "SendIGCMessage: Command=STATUS|To=BaseBETA1|From=NaveBETA1|Status=Idle|Origin=|OriginPosition=0:0:0|Destination=|DestinationPosition=0:0:0|Position=-50030.1467906485:-87218.8541785618:-43613.1279604134"
            ];
            string customData = @"REQUEST_STATUS
";

            var blackList = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (blackList.Length > 0)
            {
                string[] lines = logLines.Where(l => !blackList.Any(b => l.Contains(b))).ToArray();
                Console.WriteLine(string.Join(Environment.NewLine, lines));
            }
            else
            {
                Console.WriteLine(string.Join(Environment.NewLine, logLines));
            }

            Console.ReadLine();
        }
    }
}