using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace NormandErwan.DocFxForUnity
{
    /// <summary>
    /// Generates the xref map of all Unity versions and commit them to the `gh-pages` branch of
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
        private const string GeneratedDocsPath = "_site";

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
        /// Entry point of this program.
        /// </summary>
        public static void Main()
        {
            using (var ghPagesRepo = GetSyncRepository(GhPagesRepoUrl, GhPagesRepoPath, GhPagesRepoBranch))
            {
                using (var unityRepo = GetSyncRepository(UnityRepoUrl, UnityRepoPath))
                {
                    GenerateXrefMaps(unityRepo);
                    CopyVersionXrefMaps(unityRepo);
                }

                AddCommitChanges(ghPagesRepo, "Xrefmaps update", CommitIdentity);
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
        /// Copies the xref map of each Unity version (`YYYY.X.Y`), each major Unity version (`YYYY.X`) from the latest
        /// corresponding release to their dedicated folder, and the latest release of the latest version to the root
        /// folder.
        /// </summary>
        /// <param name="repository">The Unity repository to use.</param>
        /// <param name="directoryPath">The directory where the xref maps have been generated.</param>
        private static void CopyVersionXrefMaps(Repository repository, string directoryPath = XrefMapsPath)
        {
            var versions = GetLatestReleases(repository, @"[abfp]");

            var majorVersions = GetLatestReleases(repository, @"\.\d+[abfp]");
            versions = versions.Union(majorVersions);

            var latestStableVersion = majorVersions
                .OrderByDescending(version => version.name)
                .First(version => version.release.Contains('f'));
            latestStableVersion.name = ".";
            versions = versions.Append(latestStableVersion);

            versions = versions.OrderByDescending(version => version.name);
            foreach (var version in versions)
            {
                Console.WriteLine($"Copy {directoryPath}/{version.release}/xrefmap.yml to {directoryPath}/{version.name}");
                CopyXrefMap(version.release, version.name, directoryPath);
            }
        }

        /// <summary>
        /// Copy a source xref map from to a destination directory path.
        /// </summary>
        /// <param name="sourceCommit">The commit of the source xref map.</param>
        /// <param name="destCommit">The commit where to copy the source xref map.</param>
        /// <param name="directoryPath">The directory where the xref maps have been generated.</param>
        private static void CopyXrefMap(string sourceCommit, string destCommit, string directoryPath = XrefMapsPath)
        {
            string sourceXrefMapPath = GetXrefMapPath(sourceCommit, directoryPath);
            string destXrefMapPath = GetXrefMapPath(destCommit, directoryPath);
            CopyFile(sourceXrefMapPath, destXrefMapPath);
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

                Console.WriteLine($"Fetching changes from origin to {path}");
                var remote = repository.Network.Remotes["origin"];
                Commands.Fetch(repository, remote.Name, new string[0], null, null); // WTF is this API libgit2sharp?

                Console.WriteLine($"Checking out {path} to {branch} branch");
                Commands.Checkout(repository, branch);
            }

            return repository;
        }

        /// <summary>
        /// Generates the xref map of each Unity release.
        /// </summary>
        /// <param name="repository">The Unity repository to use.</param>
        /// <param name="directoryPath">The directory where to copy the generated xref maps.</param>
        private static void GenerateXrefMaps(Repository repository, string directoryPath = XrefMapsPath)
        {
            foreach (var tag in repository.Tags)
            {
                string release = tag.FriendlyName;
                string destXrefMapPath = GetXrefMapPath(release, directoryPath);

                if (File.Exists(destXrefMapPath))
                {
                    Console.WriteLine($"Skip generating Unity {release}'s xref: already present on {directoryPath}");
                }
                else
                {
                    Console.WriteLine($"Generating Unity {release} xref");
                    GenerateXrefMap(repository, release);

                    string sourceXrefMapPath = Path.Combine(GeneratedDocsPath, XrefMapFileName);
                    CopyFile(sourceXrefMapPath, destXrefMapPath);
                }
            }
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
            repository.Reset(ResetMode.Hard, commit);

            if (Directory.Exists(generatedDocsPath))
            {
                Directory.Delete(generatedDocsPath, recursive: true);
            }

            RunCommand($"docfx");
        }

        /// <summary>
        /// Returns a collection of the latest tags of a specified repository grouped by with a regex pattern. Sort is
        /// done by date of the tag's commit.
        /// </summary>
        /// <param name="repository">The repository to use.</param>
        /// <param name="splitPattern">
        /// The regex pattern to apply to each repository tag. The left part of the split will be used as tuple's
        /// name.
        /// </param>
        /// <returns>
        /// A collection of tuples containing the left part of the split as tuple's name and the latest tag's name
        /// matching this tuple's name.
        /// </returns>
        private static IEnumerable<(string name, string release)> GetLatestReleases(Repository repository,
            string splitPattern)
        {
            return repository.Tags
                .OrderByDescending(tag => (tag.Target as Commit).Author.When)
                .Select(tag =>
                {
                    string release = tag.FriendlyName;
                    string name = Regex.Split(release, splitPattern)[0];
                    return (name, release);
                })
                .GroupBy(version => version.name)
                .Select(g => g.First());
        }

        /// <summary>
        /// Returns the file path of a generated xref map of a specified commit of a repository.
        /// </summary>
        /// <param name="commit">The commit of the xref map.</param>
        /// <param name="directoryPath">The directory where the commit has been generated.</param>
        /// <returns>The file path of the xref map.</returns>
        private static string GetXrefMapPath(string commit, string directoryPath = XrefMapsPath)
        {
            string xrefMapsDirectoryPath = Path.Combine(directoryPath, commit);
            return Path.Combine(xrefMapsDirectoryPath, XrefMapFileName);
        }

        /// <summary>
        /// Run a command in a hidden window and returns its output.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <returns>The output of the command.</returns>
        private static string RunCommand(string command)
        {
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();
            process.Dispose();

            return output;
        }
    }
}
