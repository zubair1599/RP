using System;
using System.Reflection;
using log4net;
using log4net.Config;

[assembly: XmlConfigurator(Watch = true)]

namespace RPDailyScrape
{
    public class Logger
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static Object log_lock = new Object();

        public static void WriteLog(string message)
        {
            lock (log_lock)
            {
                if (log.IsInfoEnabled) log.Info(message);
            }
        }
    }
}