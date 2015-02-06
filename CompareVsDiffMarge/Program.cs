using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;

namespace CompareVsDiffMarge
{
    class Program
    {
        private const string PipeName = "CompareVsDiffMargePipe";

        static void Main(string[] args)
        {
            bool isAdmin;
            string file;

            ParseArguments(args, out isAdmin, out file);

            if (CreateParentProcessStream(isAdmin, file))
                return;

            NotifyParentProcess(file);
        }

        private static void ParseArguments(IList<string> args, out bool isAdmin, out string file)
        {
            isAdmin = false;
            if(args == null)
            {
                throw new ArgumentNullException("args");
            }
            if(args.Count > 2)
            {
                throw new InvalidOperationException("Only 1 or 2 arguments accepted");
            }
            if(args.Count == 2)
            {
                isAdmin = args.Any(a => a == "/a");
                file = args.First(a => a != "/a");
            }
            else
            {
                file = args[0];
            }
        }

        private static bool NotifyParentProcess(string secondFile)
        {
            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
            {
                try
                {
                    pipe.Connect(100);
                    Console.WriteLine("connected {0}", pipe.NumberOfServerInstances);
                }
                catch (TimeoutException)
                {
                    return false;
                }
                if (!pipe.IsConnected)
                    return false;

                using (var writer = new StreamWriter(pipe))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(secondFile);
                    pipe.WaitForPipeDrain();
                }
                pipe.Close();
            }
            return true;
        }

        private static bool CreateParentProcessStream(bool isAdmin, string firstFile)
        {
            try
            {
                using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In))
                {
                    Console.WriteLine("Starting server");
                    pipe.WaitForConnection();
                    using (var reader = new StreamReader(pipe))
                    {
                        var secondFile = reader.ReadToEnd();
                        var path =
                            Path.GetFullPath(Path.Combine(Environment.ExpandEnvironmentVariables("%VS120COMNTOOLS%"),
                            //@"..\IDE\vsdiffmerge.exe"));
                                @"..\IDE\devenv.exe"));
                        var startInfo = new ProcessStartInfo(string.Format("\"{0}\"", path))
                                        {
                                            UseShellExecute = true,
                                            Arguments =
                                                //string.Format("\"{0}\" \"{1}\"", firstFile.Trim(), secondFile.Trim())
                                                string.Format("/diff \"{0}\" \"{1}\"", firstFile.Trim(), secondFile.Trim()),
                                            WorkingDirectory = Environment.CurrentDirectory,
                                        };
                        if(isAdmin)
                        {
                            startInfo.Verb = "runas";
                        }

                        Console.WriteLine("Starting diff tool");
                        Process.Start(startInfo);
                        //process.WaitForExit();
                    }
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
