using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace NormandErwan.DocFxForUnity
{
    /// <summary>
    /// Generates xref map from all Unity versions then commit them to `gh-pages` branch of
    /// https://github.com/NormandErwan/DocFxForUnity.
    ///
    /// Usage: UnityXrefMaps
    /// </summary>
    /// <remarks>.NET Core 2.x, Git and DocFx must be installed on your system.</remarks>
    class Program
    {
        /// <summary>
        /// File path where the documentation of the Unity repo will be generated.
        /// </summary>
        private const string GeneratedDocsPath = "_site";

        /// <summary>
        /// Name of the branch where to commit the xref maps.
        /// </summary>
        private const string GhPagesRepoBranch = "gh-pages";

        /// <summary>
        /// File path of the repository where to commit the xref maps.
        /// </summary>
        private const string GhPagesRepoPath = "gh-pages";

        /// <summary>
        /// Url of the repository where to commit the xref maps.
        /// </summary>
        private const string GhPagesRepoUrl = "https://github.com/NormandErwan/DocFxForUnity.git";

        /// <summary>
        /// The identity to use to commit to the gh-pages branch.
        /// </summary>
        private static readonly Identity CommitIdentity = new Identity("Erwan Normand", "normand.erwan@protonmail.com");

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
        /// Entry point of this program.
        /// </summary>
        private static void Main()
        {
            using (var ghPagesRepo = GetSyncRepository(GhPagesRepoUrl, GhPagesRepoPath, GhPagesRepoBranch))
            {
                using (var unityRepo = GetSyncRepository(UnityRepoUrl, UnityRepoPath))
                {
                    GenerateReleaseXrefMaps(unityRepo);
                    GenerateVersionXrefMaps(unityRepo);
                }

                CommitAndPush(ghPagesRepo);
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
        /// Fetches changes and hard resets the specified repository to the latest commit of a specified branch. If no
        /// repository is found, it will be cloned before.
        /// </summary>
        /// <param name="sourceUrl">The url of the repository.</param>
        /// <param name="path">The directory path where to find/clone the repository.</param>
        /// <param name="branch">The branch use on the repository.</param>
        /// <returns>The synced repository on the latest commit of the specified branch.</returns>
        private static Repository GetSyncRepository(string sourceUrl, string path, string branch = "master")
        {
            // Clone this repo if it doesn't exist
            bool clone = !Directory.Exists(path);
            if (clone)
            {
                Console.WriteLine($"Clonning {sourceUrl} to {path}");
                Repository.Clone(sourceUrl, path);
            }

            var repository = new Repository(path);

            // Otherwise fetch changes
            if (!clone)
            {
                Console.WriteLine($"Fetching changes from {sourceUrl} to {path}");

                var remote = repository.Network.Remotes["origin"];
                Commands.Fetch(repository, remote.Name, new string[0], null, null); // WTF is this API libgit2sharp?
            }

            // Reset to the specified branch
            Console.WriteLine($"Hard reset {path} to {branch} branch");

            var remoteBranch = repository.Branches[$"origin/{branch}"];
            repository.Reset(ResetMode.Hard, remoteBranch.Tip);

            return repository;
        }

        /// <summary>
        /// Generates xref map of each Unity release.
        /// </summary>
        /// <param name="repository">The Unity repository.</param>
        private static void GenerateReleaseXrefMaps(Repository repository)
        {
            foreach (var tag in repository.Tags)
            {
                string release = tag.FriendlyName;
                string destXrefMapPath = GetXrefMapPath(release);

                if (File.Exists(destXrefMapPath))
                {
                    Console.WriteLine($"Skip generating Unity {release} docs: corresponding xrefmap"
                        + " already present on the repo");
                }
                else
                {
                    Console.WriteLine($"Generating Unity {release} docs.");
                    string sourceXrefMapPath = GenerateXrefMap(repository, release, GhPagesRepoPath);

                    Console.WriteLine($"Copy {sourceXrefMapPath} to {destXrefMapPath}");
                    CopyFile(sourceXrefMapPath, destXrefMapPath);
                }
            }
        }

        /// <summary>
        /// Copies the xref map of each Unity version from the latest corresponding release.
        /// </summary>
        /// <param name="repository">The Unity repository.</param>
        private static void GenerateVersionXrefMaps(Repository repository)
        {
            var versions = repository.Tags
                .OrderByDescending(tag => (tag.Target as Commit).Author.When)
                .Select(tag =>
                {
                    string release = tag.FriendlyName;
                    string name = Regex.Split(release, "[abfp]")[0];
                    return (name, release);
                })
                .GroupBy(version => version.name)
                .Select(g => g.First());

            foreach (var version in versions)
            {
                string sourceXrefMapPath = GetXrefMapPath(version.release);
                string destXrefMapPath = GetXrefMapPath(version.name);

                Console.WriteLine($"Copy {sourceXrefMapPath} to {destXrefMapPath}");
                CopyFile(sourceXrefMapPath, destXrefMapPath);
            }
        }

        /// <summary>
        /// Generate documentation and returns the associated xref map of a specified repository with DocFx.
        /// </summary>
        /// <param name="repository">The repository to generate docs from.</param>
        /// <param name="commit">The commit of the <param name="repository"> to generate the docs from.</param>
        /// <param name="generatedDocsPath">
        /// The directory where the docs will be generated (`output` property of `docfx build`).
        /// </param>
        /// <returns>The file path of the generated xrefmap.</returns>
        private static string GenerateXrefMap(Repository repository, string commit,
            string generatedDocsPath = GeneratedDocsPath)
        {
            repository.Reset(ResetMode.Hard, commit);

            if (Directory.Exists(generatedDocsPath))
            {
                Directory.Delete(generatedDocsPath, recursive: true);
            }

            RunCommand($"docfx");

            return Path.Combine(GeneratedDocsPath, XrefMapFileName);
        }

        /// <summary>
        /// Returns the file path of a generated xref map of a specified commit of a repository.
        /// </summary>
        /// <param name="commit">The commit of the xref map.</param>
        /// <param name="directoryPath">The directory where the commit has been generated.</param>
        /// <returns>The file path of the xref map.</returns>
        private static string GetXrefMapPath(string commit, string directoryPath = GhPagesRepoPath)
        {
            string xrefMapsDirectoryPath = Path.Combine(directoryPath, commit);
            return Path.Combine(xrefMapsDirectoryPath, XrefMapFileName);
        }

        /// <summary>
        /// Add, commit and push all changes on the specified repository.
        /// </summary>
        /// <param name="repo">The repository to add, command and push changes.</param>
        private static void CommitAndPush(Repository repo)
        {
            if (repo.RetrieveStatus().IsDirty)
            {
                Console.WriteLine($"Add, commit and push all changes on {GhPagesRepoPath}.");

                Commands.Stage(repo, "*");

                var author = new Signature(CommitIdentity, DateTime.Now);
                var committer = author;
                repo.Commit("Xrefmaps update", author, committer);

                // TODO push
            }
            else
            {
                Console.WriteLine($"Nothing to commit on {GhPagesRepoPath}.");
            }
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
