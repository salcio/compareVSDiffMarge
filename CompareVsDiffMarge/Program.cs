using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompareVsDiffMarge
{
    class Program
    {
        private const string PipeName = "CompareVsDiffMargePipe";

        static void Main(string[] args)
        {
            if (CreateParentProcessStream(args[0]))
                return;

            NotifyParentProcess(args[0]);
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

        private static bool CreateParentProcessStream(string firstFile)
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
                                            UseShellExecute = false,
                                            Arguments =
                                                //string.Format("\"{0}\" \"{1}\"", firstFile.Trim(), secondFile.Trim())
                                                string.Format("/diff \"{0}\" \"{1}\"", firstFile.Trim(), secondFile.Trim())
                                        };
                        var process = Process.Start(startInfo);
                        process.WaitForExit();
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
