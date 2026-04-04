using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor.PackageManager;

namespace Design2Ugui.Core
{
    public class UnityPenFileGenerator
    {
        public string GeneratePenFile(
            string componentBundlePath,
            string screenBundlePath,
            string auditBundlePath,
            string outputPenPath,
            string nodeExecutable = "node")
        {
            ValidateInput(componentBundlePath, nameof(componentBundlePath));
            ValidateInput(screenBundlePath, nameof(screenBundlePath));
            ValidateInput(auditBundlePath, nameof(auditBundlePath));

            if (string.IsNullOrWhiteSpace(outputPenPath))
            {
                throw new ArgumentException("Output .pen path is required.", nameof(outputPenPath));
            }

            var packageRoot = ResolvePackageRoot();
            var scriptPath = Path.Combine(packageRoot, "scripts", "write-pen-file.mjs");
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("Pen file writer script not found.", scriptPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPenPath) ?? Directory.GetCurrentDirectory());

            var arguments = new StringBuilder();
            arguments.Append(Quote(scriptPath));
            arguments.Append(" --components ").Append(Quote(componentBundlePath));
            arguments.Append(" --screens ").Append(Quote(screenBundlePath));
            arguments.Append(" --audit ").Append(Quote(auditBundlePath));
            arguments.Append(" --out ").Append(Quote(outputPenPath));

            var startInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(nodeExecutable) ? "node" : nodeExecutable,
                Arguments = arguments.ToString(),
                WorkingDirectory = packageRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Node.js process.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Pen file generation failed with exit code {process.ExitCode}.{Environment.NewLine}{stderr}".Trim()
                );
            }

            if (File.Exists(outputPenPath))
            {
                return outputPenPath;
            }

            var outputPath = stdout.Trim();
            if (!string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
            {
                return outputPath;
            }

            throw new InvalidOperationException("Pen file generation completed without producing an output file.");
        }

        private static void ValidateInput(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"Missing required path: {parameterName}", parameterName);
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required bundle file not found.", path);
            }
        }

        private static string ResolvePackageRoot()
        {
            var packageInfo = PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            if (packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                throw new InvalidOperationException("Unable to resolve package root for pencil2ugui.");
            }

            return packageInfo.resolvedPath;
        }

        private static string Quote(string value)
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
