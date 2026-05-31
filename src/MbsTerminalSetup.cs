using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MbsTerminalSetup
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var executablePath = typeof(Program).Assembly.Location;
            var repositoryRoot = Path.GetDirectoryName(executablePath);

            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                Console.Error.WriteLine("Could not determine installer directory.");
                return 1;
            }

            var installerPath = Path.Combine(repositoryRoot, "install.ps1");

            if (!File.Exists(installerPath))
            {
                Console.Error.WriteLine("install.ps1 was not found next to this executable.");
                Console.Error.WriteLine("Run MBS-Terminal-Setup.exe from the MBS-Terminal repository folder.");
                return 1;
            }

            var powershellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"
            );

            var arguments = new List<string>
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Quote(installerPath)
            };

            foreach (var argument in args)
            {
                arguments.Add(Quote(argument));
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Console.Error.WriteLine("Could not start PowerShell installer.");
                    return 1;
                }

                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
