using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor.PackageManager;

namespace Design2Ugui.Core
{
    public class PencilBundleGenerator
    {
        public string GenerateBundle(string penFilePath, string nodeId, string outputRoot, string nodeExecutable = "node")
        {
            if (string.IsNullOrWhiteSpace(penFilePath))
            {
                throw new ArgumentException("Pencil file path is required.", nameof(penFilePath));
            }

            if (!File.Exists(penFilePath))
            {
                throw new FileNotFoundException("Pencil file not found.", penFilePath);
            }

            var packageRoot = ResolvePackageRoot();
            var scriptPath = Path.Combine(packageRoot, "scripts", "pencil-to-unity-bundle.mjs");
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("Bundle generation script not found.", scriptPath);
            }

            var bundleDirectory = Path.Combine(Path.GetTempPath(), "pencil2ugui");
            Directory.CreateDirectory(bundleDirectory);

            var bundlePath = Path.Combine(
                bundleDirectory,
                $"{Path.GetFileNameWithoutExtension(penFilePath)}_{SanitizeSegment(nodeId)}_{Guid.NewGuid():N}.bundle.json"
            );

            var arguments = new StringBuilder();
            arguments.Append(Quote(scriptPath));
            arguments.Append(" --file ").Append(Quote(penFilePath));

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                arguments.Append(" --node ").Append(Quote(nodeId));
            }

            if (!string.IsNullOrWhiteSpace(outputRoot))
            {
                arguments.Append(" --output-root ").Append(Quote(outputRoot));
            }

            arguments.Append(" --out ").Append(Quote(bundlePath));

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
                    $"Bundle generation failed with exit code {process.ExitCode}.{Environment.NewLine}{stderr}".Trim()
                );
            }

            if (!File.Exists(bundlePath))
            {
                var outputPath = stdout.Trim();
                if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
                {
                    throw new InvalidOperationException("Bundle generation completed without producing an output file.");
                }

                return outputPath;
            }

            return bundlePath;
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

        private static string SanitizeSegment(string value)
        {
            var raw = string.IsNullOrWhiteSpace(value) ? "root" : value.Trim();
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(invalidChar, '_');
            }

            return raw.Replace(' ', '_');
        }
    }
}
