using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RegAsmProxy
{
    class Program
    {
        const string Regasm32 = @"c:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe";
        const string Regasm64 = @"c:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe";

        static void Main(string[] args)
        {
            //This is required to get proper "" around the filename - if I specify them in the custom action they got lost.
            string fullParams = string.Join(" ", args).Replace("'","\"");
           
            Run(Regasm32, fullParams);
            Run(Regasm64, fullParams);
        }

        private static void Run(string toolPath, string paramString)
        {

            if (File.Exists(toolPath))
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = toolPath,
                        Arguments = paramString,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = false
                    }
                };
                proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                    Debug.WriteLine(line);
                }
            }
        }
    }
}
