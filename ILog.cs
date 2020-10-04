using System.Collections.Generic;

/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 10/01/2020
*/

namespace OPC_UA.NET_stack_wrapper
{
    /// <summary>
    /// ILog is the default Interface that the classes utilize to log exceptions, info message etc.
    /// </summary>
    public interface ILog
    {
        // implement to provide a way of adjusting the applicaple log level
        // ALL allows all levels to be printed
        public void SetLogLevel(List<LogLevel> logLevel);

        // Implement log to determine what happens with the message,
        // DefaultLog implements Ilog and prints to Console
        public void Log(LogLevel tag, string message);
    }
}
