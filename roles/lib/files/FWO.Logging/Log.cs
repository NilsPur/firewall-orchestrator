using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FWO.Logging
{
    public static class Log
    {
        private static SemaphoreSlim semaphore = new (1, 1);
        private static readonly string lockFilePath = $"/var/fworch/lock/{Assembly.GetEntryAssembly()?.GetName().Name}_log.lock";
        private static readonly Random random = new ();

        static Log()
        {
            Task.Factory.StartNew(async () =>
            {
                // log switch - log file locking
                bool logOwnedByExternal = false;
                Stopwatch stopwatch = new ();

                while (true)
                {
                    try
                    {
                        // Open file
                        using FileStream file = await GetFile(lockFilePath);
                        // Read file content
                        using StreamReader reader = new (file);
                        string lockFileContent = (await reader.ReadToEndAsync()).Trim();

                        // Forcefully release lock after timeout
                        if (logOwnedByExternal && stopwatch.ElapsedMilliseconds > 10_000)
                        {
                            using StreamWriter writer = new (file);
                            await writer.WriteLineAsync("FORCEFULLY RELEASED");
                            stopwatch.Reset();
                            semaphore.Release();
                            logOwnedByExternal = false;
                        }
                        // GRANTED - lock was granted by us
                        else if (lockFileContent.EndsWith("GRANTED"))
                        {
                            // Request lock if it is not already requested by us
                            // (in case of restart with log already granted)
                            if (!logOwnedByExternal)
                            {
                                semaphore.Wait();
                                stopwatch.Restart();
                                logOwnedByExternal = true;
                            }
                        }
                        // REQUESTED - lock was requested by log swap process
                        else if (lockFileContent.EndsWith("REQUESTED"))
                        {
                            // only request lock if it is not already requested by us
                            if (!logOwnedByExternal)
                            {
                                semaphore.Wait();
                                stopwatch.Restart();
                                logOwnedByExternal = true;
                            }
                            using StreamWriter writer = new (file);
                            await writer.WriteLineAsync("GRANTED");
                        }
                        // RELEASED - lock was released by log swap process
                        else if (lockFileContent.EndsWith("RELEASED"))
                        {
                            // only release lock if it was formerly requested by us
                            if (logOwnedByExternal) 
                            {
                                stopwatch.Reset();
                                semaphore.Release();
                                logOwnedByExternal = false;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //WriteError("Log file locking", "Error while accessing log lock file.", e);
                    }
                    await Task.Delay(1000);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static async Task<FileStream> GetFile(string path)
        {
            while (true)
            {
                try
                {
                    return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception) 
                { 
                    //WriteDebug("Log file locking", $"Could not access log lock file: {e.Message}.");
                }
                await Task.Delay(random.Next(100));
            }
        }

        [Conditional("DEBUG")]
        public static void WriteDebug(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Debug", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.White);
        }

        public static void WriteInfo(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Info", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.Cyan);
        }

        public static void WriteWarning(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Warning", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.DarkYellow);
        }

        public static void WriteError(string Title, string? Text = null, Exception? Error = null, string? User = null, string? Role = null, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string DisplayText =
                (User != null ? $"User: {User}, " : "") +
                (Role != null ? $"Role: {Role}, " : "") +
                (Text != null ? $"{Text}" : "") +
                (Error != null ?
                "\n ---\n" +
                $"Exception thrown: \n {Error?.GetType().Name} \n" +
                $"Message: \n {Error?.Message.TrimStart()} \n" +
                $"Stack Trace: \n {Error?.StackTrace?.TrimStart()}"
                : "");

            WriteLog("Error", Title, DisplayText, callerName, callerFile, callerLineNumber, ConsoleColor.Red);
        }

        public static void WriteError(string Title, string Text, bool LogStackTrace, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string DisplayText =
                (Text != null ?
                $"{Text}"
                : "") +
                (LogStackTrace ?
                "\n ---\n" +
                $"Stack Trace: \n {Environment.StackTrace}"
                : "");

            WriteLog("Error", Title, DisplayText, callerName, callerFile, callerLineNumber, ConsoleColor.Red);
        }

        /// <summary>
        /// Writes an audit log entry with the specified title and text.
        /// Optionally appends a separator line to the log entry.
        /// </summary>
        /// <param name="Title">The title of the audit log entry.</param>
        /// <param name="Text">The content of the audit log entry.</param>
        /// <param name="WithSeparatorLine">Whether to append a separator line to the log entry. Default is true.</param>
        /// <param name="callerName">The name of the calling method (automatically supplied).</param>
        /// <param name="callerFile">The file path of the calling method (automatically supplied).</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called (automatically supplied).</param>
        public static void WriteAudit(string Title, string Text, bool WithSeparatorLine = true, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            if(WithSeparatorLine)
            {
                Text += $"{Environment.NewLine}----{Environment.NewLine}";
            }

            WriteLog("Audit", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.Yellow);
        }

        /// <summary>
        /// Writes an audit log entry with the specified title, text, user name, and user distinguished name (DN).
        /// Optionally appends a separator line to the log entry.
        /// </summary>
        /// <param name="Title">The title of the audit log entry.</param>
        /// <param name="Text">The content of the audit log entry.</param>
        /// <param name="UserName">The name of the user performing the action.</param>
        /// <param name="UserDN">The distinguished name (DN) of the user.</param>
        /// <param name="WithSeparatorLine">Whether to append a separator line to the log entry. Default is true.</param>
        /// <param name="callerName">The name of the calling method (automatically supplied).</param>
        /// <param name="callerFile">The file path of the calling method (automatically supplied).</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called (automatically supplied).</param>
        public static void WriteAudit(string Title, string Text, string UserName, string UserDN, bool WithSeparatorLine = true, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            if(!string.IsNullOrEmpty(UserName))
            {
                Text += $" by User: {UserName}";
            }

            if(!string.IsNullOrEmpty(UserDN))
            {
                Text += $" (DN: {UserDN})";
            }

            if(WithSeparatorLine)
            {
                Text += $"{Environment.NewLine}----{Environment.NewLine}";
            }

            WriteLog("Audit", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.Yellow);
        }

        private static void WriteLog(string LogType, string Title, string Text, string Method, string Path, int Line, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null)
        {
            string File = Path.Split('\\', '/').Last(); // do not show the full file path, just the basename
            WriteInColor($"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")} {LogType} - {Title} ({File} in line {Line}), {Text}", ForegroundColor, BackgroundColor);
        }

        public static void WriteAlert(string Title, string Text)
        {
            // fixed format to be further processed (e.g. splunk)
            WriteInColor($"FWORCHAlert - {Title}, {Text}");
        }

        private static void WriteInColor(string Text, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null)
        {
            semaphore.Wait();
            if (ForegroundColor != null)
                Console.ForegroundColor = (ConsoleColor)ForegroundColor;
            if (BackgroundColor != null)
                Console.BackgroundColor = (ConsoleColor)BackgroundColor;
            Console.Out.WriteLine(Text); // TODO: async method ?
            Console.ResetColor();
            semaphore.Release();
        }
    }
}
