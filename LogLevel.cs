/**
* @author Emil Stubbe Kolvig-Raun
* @email emilkolvigraun@gmail.com
* @date - 10/01/2020
*/

namespace OPC_UA.NET_stack_wrapper
{
    /// <summary>
    /// LogLevel provides a number of LogLevels to be used as reference.
    /// </summary>
    public enum LogLevel
    {
        ALL     = 0, // automatically accept all log message tags.
        NONE    = 1, // automatically refuse all log message tags.
        DEBUG   = 2, // used for debugging.
        MSG     = 3, // used for subscription message by defauly, but method should overwitten by developer.
        INFO    = 4, // used for info messages.
        WARN    = 5, // used for warning messages.
        ERROR   = 6, // used for error messages.
        FATAL   = 7  // used for errors that are critical to successful operation.
    }
}  
