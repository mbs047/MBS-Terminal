using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace MbsTerminalInstall
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (!IsAdministrator() && !HasSwitch(args, "NoAdminRelaunch") && !HasSwitch(args, "DryRun"))
            {
                return RelaunchAsAdministrator(args);
            }

            string supportRoot = ResolveSupportRoot();
            bool extractedSupportRoot = false;

            try
            {
                if (string.IsNullOrWhiteSpace(supportRoot))
                {
                    supportRoot = CreateExtractionRoot();
                    EmbeddedTerminalInstallerSupportFiles.ExtractTo(supportRoot);
                    extractedSupportRoot = true;
                }

                string installScript = Path.Combine(supportRoot, "install-terminal.ps1");

                if (!File.Exists(installScript))
                {
                    WriteError("install-terminal.ps1 was not found.");
                    return 1;
                }

                return RunPowerShellInstaller(supportRoot, installScript, args);
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
                WaitForEnter();
                return 1;
            }
            finally
            {
                if (extractedSupportRoot)
                {
                    TryDeleteDirectory(supportRoot);
                }
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static int RelaunchAsAdministrator(string[] args)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = typeof(Program).Assembly.Location;
                startInfo.Arguments = BuildArgumentString(args);
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                Process.Start(startInfo);
                return 0;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                WriteError("Administrator permission is required to run MBS Terminal Install.");
                WaitForEnter();
                return ex.NativeErrorCode == 1223 ? 1 : ex.NativeErrorCode;
            }
            catch (Exception ex)
            {
                WriteError("Could not restart as administrator: " + ex.Message);
                WaitForEnter();
                return 1;
            }
        }

        private static string ResolveSupportRoot()
        {
            string executablePath = typeof(Program).Assembly.Location;
            string executableDirectory = Path.GetDirectoryName(executablePath);

            if (!string.IsNullOrWhiteSpace(executableDirectory)
                && File.Exists(Path.Combine(executableDirectory, "install-terminal.ps1"))
                && File.Exists(Path.Combine(executableDirectory, "install.ps1")))
            {
                return executableDirectory;
            }

            return string.Empty;
        }

        private static string CreateExtractionRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "MBS-Terminal-Install-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Process.GetCurrentProcess().Id
            );

            Directory.CreateDirectory(root);
            return root;
        }

        private static int RunPowerShellInstaller(string supportRoot, string installScript, string[] args)
        {
            List<string> installerArguments = new List<string>();
            installerArguments.Add("-NoProfile");
            installerArguments.Add("-ExecutionPolicy");
            installerArguments.Add("Bypass");
            installerArguments.Add("-File");
            installerArguments.Add(Quote(installScript));

            foreach (string argument in args)
            {
                installerArguments.Add(Quote(argument));
            }

            if (args.Length == 0 && !HasSwitch(args, "WaitAtEnd"))
            {
                installerArguments.Add("-WaitAtEnd");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = GetPowerShellPath();
            startInfo.Arguments = string.Join(" ", installerArguments.ToArray());
            startInfo.WorkingDirectory = supportRoot;
            startInfo.UseShellExecute = false;

            Process process = Process.Start(startInfo);

            if (process == null)
            {
                WriteError("Could not start PowerShell.");
                WaitForEnter();
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }

        private static string GetPowerShellPath()
        {
            string powershellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"
            );

            if (File.Exists(powershellPath))
            {
                return powershellPath;
            }

            return "powershell.exe";
        }

        private static bool HasSwitch(string[] args, string name)
        {
            foreach (string argument in args)
            {
                if (string.Equals(argument, "-" + name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "/" + name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildArgumentString(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return string.Empty;
            }

            List<string> quotedArguments = new List<string>();

            foreach (string argument in args)
            {
                quotedArguments.Add(Quote(argument));
            }

            return string.Join(" ", quotedArguments.ToArray());
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value.Length == 0 || value.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0)
            {
                return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }

            return value;
        }

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("MBS Terminal Install failed");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private static void WaitForEnter()
        {
            Console.WriteLine();
            Console.Write("Press Enter to close...");
            Console.ReadLine();
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }
    }
}
