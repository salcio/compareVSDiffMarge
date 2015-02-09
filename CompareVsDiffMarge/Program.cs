using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using CommandLine;
using CommandLine.Text;

namespace CompareVsDiffMarge
{
    public class Options
    {
        [ValueList(typeof(List<string>), MaximumElements = 1)]
        public IList<string> Values { get; set; }
        public string File => Values[0];
        [Option('a', "admin", DefaultValue = false, Required = false)]
        public bool RunAsAdmin { get; set; }
        [Option('v', "version", DefaultValue = "12", Required = false)]
        public string UseVsVersion { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    class Program
    {
        private const string PipeName = "CompareVsDiffMargePipe";

        static void Main(string[] args)
        {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options))
            {
                return;
            }

            if (CreateParentProcessStream(options))
                return;

            NotifyParentProcess(options.File);
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

        private static bool CreateParentProcessStream(Options options)
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
                            Path.GetFullPath(Path.Combine(Environment.ExpandEnvironmentVariables(string.Format("%VS{0}0COMNTOOLS%", options.UseVsVersion)),
                                //@"..\IDE\vsdiffmerge.exe"));
                                @"..\IDE\devenv.exe"));
                        var startInfo = new ProcessStartInfo(string.Format("\"{0}\"", path))
                        {
                            UseShellExecute = true,
                            Arguments =
                                                //string.Format("\"{0}\" \"{1}\"", firstFile.Trim(), secondFile.Trim())
                                                string.Format("/diff \"{0}\" \"{1}\"", options.File.Trim(), secondFile.Trim()),
                            WorkingDirectory = Environment.CurrentDirectory,
                        };
                        if (options.RunAsAdmin)
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
