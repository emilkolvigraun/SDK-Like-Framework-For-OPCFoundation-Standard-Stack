using System;
using System.Collections.Generic;

/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 10/01/2020
*/

namespace OPC_UA.NET_stack_wrapper
{
    /// <summary>
    /// The DefaultLog implements ILog and prints log messages to Console.
    /// </summary>
    public class DefaultLog : ILog
    {
        // set default log level to allow ALL messages
        public static List<LogLevel> CurrentLogLevel = new List<LogLevel>(){ LogLevel.ALL };

        // assign datetime to subtract from total to acquire current time
        public static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// CurrentTimeMillis is used to obtain the current time in milliseconds.
        /// </summary>
        /// <returns>long</returns>
        public static long CurrentTimeMillis()
        {
            // substract the 1st of January 1970 from the total amount of milliseconds, to obtain current time
            // return as long
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }

        /// <summary>
        /// Implemented from ILog to set the loglevel.
        /// <para>@logLevel : List containting LogLevel</para>
        /// <para>Example:</para>
        /// <code>MyLog.SetLogLevel( [...], {LogLevel.INFO,LogLevel.MSG});</code>
        /// </summary>
        public void SetLogLevel(List<LogLevel> logLevel)
        {
            CurrentLogLevel = logLevel; 
        }
        
        /// <summary>
        /// Log is called by the program, to handle log messages.
        /// <para>Example:</para>
        /// <code>MyLog.Log(LogLevel.INFO, "program is running as expected.");</code>
        /// </summary>
        public void Log(LogLevel tag, string message) 
        { 
            // verify that the tag is included in CurrentLogLevel, however auto accept CurrentLogLevel contains LogLevel.ALL
            // and CurerntLogLevel does not contain LogLevel.None
            if ((CurrentLogLevel.Contains(tag) || CurrentLogLevel.Contains(LogLevel.ALL)) && !CurrentLogLevel.Contains(LogLevel.NONE))

                // print to console, "timestamp | message", e.g "123456789101 | everything is running smoothly!"
                Console.WriteLine(CurrentTimeMillis() + " | " + message);
        }  
    }
}
