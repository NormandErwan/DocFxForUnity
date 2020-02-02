using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// The path where the metadata files of DocFx are located.
        /// </summary>
        private const string DocFxMetadataPath = "Temp";

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
        /// Entry point of this program.
        /// </summary>
        public static void Main()
        {
            // Clone Unity CS repository
            using (var unityRepo = Git.GetSyncRepository(UnityRepoUrl, UnityRepoPath))
            {
                // Get the list of latest stable Unity versions
                var versions = GetLatestVersions(unityRepo);

                // Generate and copy xref map to the gh-pages branch of each Unity version
                foreach (var version in versions)
                {
                    Console.WriteLine($"Generating Unity {version.name} xref map");
                    /*GenerateXrefMap(unityRepo, version.release);

                    string sourcePath = Path.Combine(GeneratedDocsPath, XrefMapFileName);
                    string destPath = Path.Combine(XrefMapsPath, version.name, XrefMapFileName);
                    Utils.CopyFile(sourcePath, destPath);

                    FixXrefMapHrefs(destPath);*/
                }

                // Set the xref map of the latest final version as the default one (copy it at the root)
                /*var latestUnityVersion = versions
                    .OrderByDescending(version => version.name)
                    .First(version => version.release.Contains('f'));

                string sourcePath = Path.Combine(XrefMapsPath, latestUnityVersion.name, XrefMapFileName);
                string destPath = Path.Combine(XrefMapsPath, XrefMapFileName);
                Utils.CopyFile(sourcePath, destPath);*/
            }
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
                    if (!Utils.TestUriExists(reference.href).Result)
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
            var pathsToClear = new string[] { DocFxMetadataPath, generatedDocsPath };
            foreach (var path in pathsToClear)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }

            // Generate site and xref map
            var output = Utils.RunCommand($"docfx");
            Console.WriteLine(output);
        }

        /// <summary>
        /// Returns a collection of the latest versions of a specified repository of Unity.
        /// </summary>
        /// <param name="unityRepository">The repository of Unity to use.</param>
        /// <returns>The collection of versions.</returns>
        private static IEnumerable<(string name, string release)> GetLatestVersions(Repository unityRepository)
        {
            return Git.GetTags(unityRepository)
                .Select(release => (name: Regex.Match(release, @"\d{4}\.\d").Value, release))
                .GroupBy(version => version.name)
                .Select(version => version.First());
        }
    }
}
