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

namespace NormandErwan.DocFxForUnity
{
    /// <summary>
    /// Generates the xref maps of all Unity versions and commit them to the `gh-pages` branch of
    /// https://github.com/NormandErwan/DocFxForUnity.
    ///
    /// Usage: UnityXrefMaps
    ///
    /// You will need to manually push commits then.
    /// </summary>
    /// <remarks>
    /// .NET Core 2.x (https://dotnet.microsoft.com), Git (https://git-scm.com/) and
    /// DocFx (https://dotnet.github.io/docfx/) must be installed on your system.
    /// </remarks>
    class Program
    {
        /// <summary>
        /// The identity to use to commit to the gh-pages branch.
        /// </summary>
        private static readonly Identity CommitIdentity = new Identity("Erwan Normand", "normand.erwan@protonmail.com");

        /// <summary>
        /// File path where the documentation of the Unity repository will be generated.
        /// </summary>
        private const string GeneratedDocsPath = "UnityCsReference/_site";

        /// <summary>
        /// Name of the branch to use on the <see cref="GhPagesRepoPath"/> repository.
        /// </summary>
        private const string GhPagesRepoBranch = "gh-pages";

        /// <summary>
        /// File path where to clone the <see cref="GhPagesRepoPath"/> repository.
        /// </summary>
        private const string GhPagesRepoPath = "gh-pages";

        /// <summary>
        /// Url of the <see cref="GhPagesRepoPath"/> repository.
        /// </summary>
        private const string GhPagesRepoUrl = "https://github.com/NormandErwan/DocFxForUnity.git";

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
            using (var ghPagesRepo = GetSyncRepository(GhPagesRepoUrl, GhPagesRepoPath, GhPagesRepoBranch))
            {
                using (var unityRepo = GetSyncRepository(UnityRepoUrl, UnityRepoPath))
                {
                    // Get the latest tag of each Unity version (YYYY.X)
                    var versions = GetLatestReleases(unityRepo, tag => Regex.Match(tag, @"\d{4}\.\d").Value);

                    // The latest stable version is at the root
                    var latestStableVersion = versions
                        .OrderByDescending(version => version.name)
                        .First(version => version.release.Contains('f'));

                    string xrefMapsPathRootDirectory = ".";
                    versions = versions.Append((xrefMapsPathRootDirectory, latestStableVersion.release));

                    // Generate and copy xref map to the gh-pages branch
                    foreach (var version in versions)
                    {
                        Console.WriteLine($"Generating Unity {version.name} xref map");
                        GenerateXrefMap(unityRepo, version.release);

                        string sourceXrefMapPath = Path.Combine(GeneratedDocsPath, XrefMapFileName);
                        string destXrefMapPath = Path.Combine(XrefMapsPath, version.name, XrefMapFileName);
                        CopyFile(sourceXrefMapPath, destXrefMapPath);

                        FixXrefMapHrefs(destXrefMapPath);
                    }

                    Console.WriteLine();
                }

                AddCommitChanges(ghPagesRepo, "Xref maps update", CommitIdentity);
            }
        }

        /// <summary>
        /// Adds and commits all changes on the specified repository.
        /// </summary>
        /// <param name="repository">The repository to add, command and push changes.</param>
        /// <param name="commitMessage">The message of the commit.</param>
        /// <param name="commitIdentity">The identity of the author and of the committer.</param>
        private static void AddCommitChanges(Repository repository, string commitMessage, Identity commitIdentity)
        {
            if (!repository.RetrieveStatus().IsDirty)
            {
                Console.WriteLine($"Nothing to commit on {GhPagesRepoPath}");
                return;
            }

            Console.WriteLine($"Adding all changes on {GhPagesRepoPath}");
            Commands.Stage(repository, "*");

            Console.WriteLine($"Commit changes on {GhPagesRepoPath}");
            var author = new Signature(commitIdentity, DateTime.Now);
            var committer = author;
            repository.Commit(commitMessage, author, committer);
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

            // Test reference hrefs exist
            var testHrefTasks = new List<Task>();
            foreach (UnityXrefMapReference reference in xrefMap.references)
            {
                reference.FixHref();

                var testUrlTask = TestUriExists(reference.href).ContinueWith(result =>
                {
                    if (result.Result == false)
                    {
                        Console.Error.WriteLine("Warning: invalid URL " + reference.href + " on " + xrefMapPath);
                    }
                });
                testHrefTasks.Add(testUrlTask);
            }
            Task.WaitAll(testHrefTasks.ToArray());

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
            Console.WriteLine($"Hard reset {repository.Info.WorkingDirectory} to {commit}");
            repository.Reset(ResetMode.Hard, commit);
            repository.RemoveUntrackedFiles();

            if (Directory.Exists(generatedDocsPath))
            {
                Directory.Delete(generatedDocsPath, recursive: true);
            }

            var output = RunCommand($"docfx");
            Console.WriteLine(output);
        }

        /// <summary>
        /// Returns a collection of the latest tags of a specified repository grouped by a name. Sort is done by date of
        /// the tag's commit.
        /// </summary>
        /// <param name="repository">The repository to use.</param>
        /// <param name="tagToName">The function to apply to get the version's name of the input tag.</param>
        /// <returns>
        /// A collection of tuples containing the version's name the latest tag matching this version.
        /// </returns>
        private static IEnumerable<(string name, string release)> GetLatestReleases(Repository repository,
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
            Console.WriteLine(clone + " " + path);
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
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }
    }
}
