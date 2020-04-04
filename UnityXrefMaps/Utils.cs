using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DocFxForUnity
{
    public sealed class Utils
    {
        /// <summary>
        /// Client for send HTTP requests and receiving HTTP responses.
        /// </summary>
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Copy a source file to a destination file. Intermediate folders will be automatically created.
        /// </summary>
        /// <param name="sourcePath">The path of the source file to copy.</param>
        /// <param name="destPath">The destination path of the copied file.</param>
        public static void CopyFile(string sourcePath, string destPath)
        {
            var destDirectoryPath = Path.GetDirectoryName(destPath);
            Directory.CreateDirectory(destDirectoryPath);

            File.Copy(sourcePath, destPath, overwrite: true);
        }

        /// <summary>
        /// Run a command in a hidden window and returns its output.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="output">The function to call with the output data of the command.</param>
        /// <param name="error">The function to call with the error data of the command.</param>
        public static void RunCommand(string command, Action<string> output, Action<string> error)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo()
                {
                    FileName = command,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.OutputDataReceived += (sender, args) => output(args.Data);
                process.ErrorDataReceived += (sender, args) => error(args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();
            }
        }

        /// <summary>
        /// Requests the specified URI with <see cref="httpClient"/> and returns if the response status code is in the
        /// range 200-299.
        /// </summary>
        /// <param name="uri">The URI to request.</param>
        /// <returns><c>true</c> if the response status code is in the range 200-299.</returns>
        public static async Task<bool> TestUriExists(string uri)
        {
            try
            {
                var headRequest = new HttpRequestMessage(HttpMethod.Head, uri);
                var response = await httpClient.SendAsync(headRequest);
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Console.Error.WriteLine($"Error: HTTP response code on {uri} is {response.StatusCode}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Exception on {uri}: {e.Message}");
                return false;
            }
        }
    }
}