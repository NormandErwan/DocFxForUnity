using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        /// Url of the repository where to commit the xref maps.
        /// </summary>
        private const string GhPagesRepoUrl = "https://github.com/NormandErwan/DocFxForUnity.git";

        /// <summary>
        /// File path of the repository where to commit the xref maps.
        /// </summary>
        private const string GhPagesRepoPath = "gh-pages";

        /// <summary>
        /// Name of the branch where to commit the xref maps.
        /// </summary>
        private const string GhPagesRepoBranch = "gh-pages";

        /// <summary>
        /// The identity to use to commit to the gh-pages branch.
        /// </summary>
        private static readonly Identity CommitIdentity = new Identity("Erwan Normand", "normand.erwan@protonmail.com");

        /// <summary>
        /// File path of the Unity repository.
        /// </summary>
        private const string UnityRepoPath = "Unity";

        /// <summary>
        /// Filename of a xref map file.
        /// </summary>
        private const string XrefMapFileName = "xrefmap.yml";

        /// <summary>
        /// Entry point of this program.
        /// </summary>
        private static void Main()
        {
            // Clone this repo to its gh-branch
            if (!Directory.Exists(GhPagesRepoPath))
            {
                Console.WriteLine($"Clonning {GhPagesRepoUrl} to {GhPagesRepoPath}.");
                Repository.Clone(GhPagesRepoUrl, GhPagesRepoPath, new CloneOptions { BranchName = GhPagesRepoBranch });
            }

            using (var ghPagesRepo = new Repository(GhPagesRepoPath))
            {
                // Force switch to gh-pages branch
                var ghPagesBranch = ghPagesRepo.Branches[GhPagesRepoBranch];
                Commands.Checkout(ghPagesRepo, ghPagesBranch);

                using (var unityRepo = new Repository(UnityRepoPath))
                {
                    // Get xref maps of each Unity version
                    foreach (var tag in unityRepo.Tags)
                    {
                        GetAndCopyXrefMap(unityRepo, tag.FriendlyName, GhPagesRepoPath);
                    }

                    // TODO #1
                }

                CommitAndPush(ghPagesRepo);
            }
        }

        /// <summary>
        /// Generate documentation with DocFx of a specified repository and commit then copies the generated xrefmap
        /// to a specified directory.
        /// </summary>
        /// <param name="repo">The repository to generate docs from.</param>
        /// <param name="commit">The commit to generate docs from.</param>
        /// <param name="outputDirectoryPath">The path of the directory where to copy the xrefmap.</param>
        /// <param name="generatedDocsPath">
        /// The directory where the docs will be generated (`output` property of `docfx build`).
        /// </param>
        /// <returns>The output of the DocFx documentation generation.</returns>
        private static void GetAndCopyXrefMap(Repository repo, string commit, string outputDirectoryPath,
            string generatedDocsPath = GeneratedDocsPath)
        {
            string xrefMapDirectoryPath = Path.Combine(outputDirectoryPath, commit);
            string xrefMapPath = Path.Combine(xrefMapDirectoryPath, XrefMapFileName);

            if (File.Exists(xrefMapPath))
            {
                Console.WriteLine($"Skip generating Unity {commit} docs: corresponding xrefmap already present on the repo.");
            }
            else
            {
                // Generate Xref Map
                Console.WriteLine($"Generating Unity {commit} docs.");

                repo.Reset(ResetMode.Hard, commit);

                if (Directory.Exists(generatedDocsPath))
                {
                    Directory.Delete(generatedDocsPath, recursive: true);
                }

                Console.WriteLine(RunCommand($"docfx"));

                // Copy Xref Map
                Directory.CreateDirectory(xrefMapDirectoryPath);

                string sourceXrefMapPath = Path.Combine(generatedDocsPath, XrefMapFileName);
                File.Copy(sourceXrefMapPath, xrefMapPath, overwrite: true);
            }
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
