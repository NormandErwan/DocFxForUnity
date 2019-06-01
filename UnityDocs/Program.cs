using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace UnityDocs
{
    /// <summary>
    /// Generates xrefmap from all Unity versions then commit them to https://github.com/NormandErwan/DocFxForUnity.
    /// </summary>
    class Program
    {
        private const string GhPagesRepoUrl = "https://github.com/NormandErwan/DocFxForUnity.git";
        private const string GhPagesRepoPath = "gh-pages";
        private const string GhPagesRepoBranch = "gh-pages";

        private const string GeneratedDocsPath = "_site";

        private const string UnityRepoPath = "Unity";

        private const string XrefMapFileName = "xrefmap.yml";

        /// <summary>
        /// Entry point of this program.
        /// </summary>
        private static void Main()
        {
            // Clone this repo
            if (!Directory.Exists(GhPagesRepoPath))
            {
                Repository.Clone(GhPagesRepoUrl, GhPagesRepoPath, new CloneOptions { BranchName = GhPagesRepoBranch });
            }

            using (var ghPagesRepo = new Repository(GhPagesRepoPath))
            {
                // Force switch to gh-pages branch
                var ghPagesBranch = ghPagesRepo.Branches[GhPagesRepoBranch];
                Commands.Checkout(ghPagesRepo, ghPagesBranch);

                // Get xref maps from Unity repo
                using (var unityRepo = new Repository(UnityRepoPath))
                {
                    foreach (var tag in unityRepo.Tags)
                    {
                        string output = GetAndCopyXrefMap(unityRepo, tag.FriendlyName, GhPagesRepoPath);
                        Console.WriteLine(output);
                    }
                }

                // Commit gh-pages branch
                if (ghPagesRepo.RetrieveStatus().IsDirty)
                {
                    Commands.Stage(ghPagesRepo, "*");

                    var author = new Signature("Erwan Normand", "@NormandErwan", DateTime.Now);
                    var committer = author;
                    ghPagesRepo.Commit("Xrefmaps update", author, committer);
                }
            }
        }

        /// <summary>
        /// Generate documentation with DocFx of a specified repository and commit then copies the generated xrefmap
        /// to a  specified directory.
        /// </summary>
        /// <param name="repo">The repository to generate docs from.</param>
        /// <param name="commit">The commit to generate docs from.</param>
        /// <param name="outputDirectoryPath">The path of the directory where to copy the xrefmap.</param>
        /// <param name="generatedDocsPath">
        /// The directory where the docs will be generated (`output` property of `docfx build`).
        /// </param>
        /// <returns>The output of the DocFx documentation generation.</returns>
        private static string GetAndCopyXrefMap(Repository repo, string commit, string outputDirectoryPath,
            string generatedDocsPath = GeneratedDocsPath)
        {
            string output = "";

            string xrefMapDirectoryPath = Path.Combine(outputDirectoryPath, commit);
            string xrefMapPath = Path.Combine(xrefMapDirectoryPath, XrefMapFileName);

            if (!File.Exists(xrefMapPath))
            {
                // Generate Xref Map
                repo.Reset(ResetMode.Hard, commit);

                if (Directory.Exists(generatedDocsPath))
                {
                    Directory.Delete(generatedDocsPath, recursive: true);
                }

                output = RunCommand($"docfx");

                // Copy Xref Map
                Directory.CreateDirectory(xrefMapDirectoryPath);

                string sourceXrefMapPath = Path.Combine(generatedDocsPath, XrefMapFileName);
                File.Copy(sourceXrefMapPath, xrefMapPath, overwrite: true);
            }
            else
            {
                output = $"Skip generating {commit} docs. Corresponding xrefmap already commited on the repo.";
            }

            return output;
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
