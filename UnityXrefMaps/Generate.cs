using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using YamlDotNet.Serialization;

namespace DocFxForUnity
{
    /// <summary>
    /// Generates the xref maps of the APIs of all the Unity versions.
    ///
    /// Usage: Generate
    ///
    /// </summary>
    /// <remarks>
    /// [.NET Core](https://dotnet.microsoft.com) >= 2.0 and [DocFX](https://dotnet.github.io/docfx/) must be installed
    /// on your system.
    /// </remarks>
    class Program
    {
        /// <summary>
        /// The path where DocFx temp files are located.
        /// </summary>
        private const string DocFxTempPath = "Temp";

        /// <summary>
        /// File path where the documentation of the Unity repository will be generated.
        /// </summary>
        private const string GeneratedDocsPath = "UnityCsReference/_site";

        /// <summary>
        /// File path of the Unity repository.
        /// </summary>
        private const string UnityRepoPath = "UnityCsReference";

        /// <summary>
        /// Url of the repository of the Unity repository.
        /// </summary>
        private const string UnityRepoUrl = "https://github.com/Unity-Technologies/UnityCsReference.git";

        /// <summary>
        /// Filename of a xref map file.
        /// </summary>
        private const string XrefMapFileName = "xrefmap.yml";

        /// <summary>
        /// Directory path where to copy the xref maps.
        /// </summary>
        private const string XrefMapsPath = "gh-pages/Unity";

        /// <summary>
        /// Client for send HTTP requests and receiving HTTP responses.
        /// </summary>
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Entry point of this program.
        /// </summary>
        public static void Main()
        {
            // Clone Unity CS repository
            using (var unityRepo = GetSyncRepository(UnityRepoUrl, UnityRepoPath))
            {
                string sourcePath, destPath;

                // Get the list of latest stable Unity versions
                var versions = GetLatestVersions(unityRepo, tag => Regex.Match(tag, @"\d{4}\.\d").Value);

                // Generate and copy xref map to the gh-pages branch of each Unity version
                foreach (var version in versions)
                {
                    Console.WriteLine($"Generating Unity {version.name} xref map");
                    GenerateXrefMap(unityRepo, version.release);

                    sourcePath = Path.Combine(GeneratedDocsPath, XrefMapFileName);
                    destPath = Path.Combine(XrefMapsPath, version.name, XrefMapFileName);
                    CopyFile(sourcePath, destPath);

                    FixXrefMapHrefs(destPath);
                }

                // Set the xref map of the latest final version as the default one (copy it at the root)
                var latestUnityVersion = versions
                    .OrderByDescending(version => version.name)
                    .First(version => version.release.Contains('f'));

                sourcePath = Path.Combine(XrefMapsPath, latestUnityVersion.name, XrefMapFileName);
                destPath = Path.Combine(XrefMapsPath, XrefMapFileName);
                CopyFile(sourcePath, destPath);
            }
        }

        /// <summary>
        /// Copy a source file to a destination file. Intermediate folders will be automatically created.
        /// </summary>
        /// <param name="sourcePath">The path of the source file to copy.</param>
        /// <param name="destPath">The destination path of the copied file.</param>
        private static void CopyFile(string sourcePath, string destPath)
        {
            var destDirectoryPath = Path.GetDirectoryName(destPath);
            Directory.CreateDirectory(destDirectoryPath);

            File.Copy(sourcePath, destPath, overwrite: true);
        }

        /// <summary>
        /// Fix the href to of the specified Unity's xref map.
        /// </summary>
        /// <param name="xrefMapPath">The Unity's xref map.</param>
        private static void FixXrefMapHrefs(string xrefMapPath)
        {
            // Remove `0:` strings on the xrefmap that make crash Deserializer
            string xrefMapText = File.ReadAllText(xrefMapPath);
            xrefMapText = Regex.Replace(xrefMapText, @"(\d):", "$1");

            // Load xref map
            var deserializer = new Deserializer();
            var xrefMap = deserializer.Deserialize<XrefMap>(xrefMapText);

            // Try to fix hrefs
            var references = new List<XrefMapReference>();
            foreach (var reference in xrefMap.references)
            {
                if (reference.TryFixHref())
                {
                    if (!TestUriExists(reference.href).Result)
                    {
                        Console.WriteLine("Warning: invalid URL " + reference.href + " for " + reference.uid +
                            " uid on " + xrefMapPath);
                    }
                    else
                    {
                        references.Add(reference);
                    }
                }
            }
            xrefMap.references = references.ToArray();

            // Save xref map
            var serializer = new Serializer();
            xrefMapText = "### YamlMime:XRefMap" + "\n"
                + serializer.Serialize(xrefMap);
            File.WriteAllText("output.yml", xrefMapText);
        }

        /// <summary>
        /// Generate the documentation and the associated xref map of a specified repository with DocFx.
        /// </summary>
        /// <param name="repository">The repository to generate docs from.</param>
        /// <param name="commit">The commit of the <param name="repository"> to generate the docs from.</param>
        /// <param name="generatedDocsPath">
        /// The directory where the documentation will be generated (`output` property of `docfx build`, by default
        /// `_site`).
        /// </param>
        private static void GenerateXrefMap(Repository repository, string commit,
            string generatedDocsPath = GeneratedDocsPath)
        {
            // Hard reset the repository
            Console.WriteLine($"Hard reset {repository.Info.WorkingDirectory} to {commit}");
            repository.Reset(ResetMode.Hard, commit);
            repository.RemoveUntrackedFiles();

            // Clear DocFx's temp files and previous generated site
            var pathsToClear = new string[] { DocFxTempPath, generatedDocsPath };
            foreach (var path in pathsToClear)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }

            // Generate site and xref map
            var output = RunCommand($"docfx");
            Console.WriteLine(output);
        }

        /// <summary>
        /// Returns a collection of the latest tags of a specified repository.
        /// </summary>
        /// <param name="repository">The repository to use.</param>
        /// <param name="tagToName">The function to apply to get the version's name of a tag.</param>
        /// <returns>
        /// A collection of tuples containing the latest tags of <paramref name="repository"/>.
        /// </returns>
        /// <remarks>
        /// Tags of the repository are grouped by name with <paramref name="tagToName"/> then sorted is done by date of
        /// the tag's commit.
        /// </remarks>
        private static IEnumerable<(string name, string release)> GetLatestVersions(Repository repository,
            Func<string, string> tagToName)
        {
            return repository.Tags
                .OrderByDescending(tag => (tag.Target as Commit).Author.When)
                .Select(tag =>
                {
                    string release = tag.FriendlyName;
                    return (name: tagToName(release), release);
                })
                .GroupBy(version => version.name)
                .Select(g => g.First());
        }

        /// <summary>
        /// Fetches changes and hard resets the specified repository to the latest commit of a specified branch. If no
        /// repository is found, it will be cloned before.
        /// </summary>
        /// <param name="sourceUrl">The url of the repository.</param>
        /// <param name="path">The directory path where to find/clone the repository.</param>
        /// <param name="branch">The branch use on the repository.</param>
        /// <returns>The synced repository on the latest commit of the specified branch.</returns>
        private static Repository GetSyncRepository(string sourceUrl, string path, string branch = "master")
        {
            // Clone this repository to the specified branch if it doesn't exist
            bool clone = !Directory.Exists(path);
            if (clone)
            {
                Console.WriteLine($"Clonning {sourceUrl} to {path}");
                Repository.Clone(sourceUrl, path, new CloneOptions() { BranchName = branch });
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
        private static string RunCommand(string command)
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
        private static async Task<bool> TestUriExists(string uri)
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
