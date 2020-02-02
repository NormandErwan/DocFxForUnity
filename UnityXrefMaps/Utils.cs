using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using LibGit2Sharp;

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
        /// Fetches changes and hard resets the specified repository to the latest commit of a specified branch. If no
        /// repository is found, it will be cloned before.
        /// </summary>
        /// <param name="sourceUrl">The url of the repository.</param>
        /// <param name="path">The directory path where to find/clone the repository.</param>
        /// <param name="branch">The branch use on the repository.</param>
        /// <returns>The synced repository on the latest commit of the specified branch.</returns>
        public static Repository GetSyncRepository(string sourceUrl, string path, string branch = "master")
        {
            // Clone this repository to the specified branch if it doesn't exist
            bool clone = !Directory.Exists(path);
            if (clone)
            {
                Console.WriteLine($"Clonning {sourceUrl} to {path}");

                var options = new CloneOptions() { BranchName = branch };
                Repository.Clone(sourceUrl, path, options);
            }

            var repository = new Repository(path);

            // Otherwise fetch changes and checkout to the specified branch
            if (!clone)
            {
                Console.WriteLine($"Hard reset {path} to HEAD");
                repository.Reset(ResetMode.Hard);
                repository.RemoveUntrackedFiles();

                Console.WriteLine($"Fetching changes from origin to {path}");
                var remote = repository.Network.Remotes["origin"];
                Commands.Fetch(repository, remote.Name, new string[0], null, null); // WTF is this API libgit2sharp?

                Console.WriteLine($"Checking out {path} to {branch} branch");
                Commands.Checkout(repository, branch);
            }

            Console.WriteLine();

            return repository;
        }

        /// <summary>
        /// Run a command in a hidden window and returns its output.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <returns>The output of the command.</returns>
        public static string RunCommand(string command)
        {
            string output = "";

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                process.Start();
                output = process.StandardOutput.ReadToEnd();

                process.WaitForExit();
            }

            return output;
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