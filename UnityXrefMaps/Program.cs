using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;

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
        /// The path where the metadata files of DocFx of the Unity repository will be generated.
        /// </summary>
        private const string DocFxMetadataPath = "Temp";

        /// <summary>
        /// The path where the documentation of the Unity repository will be generated.
        /// </summary>
        private const string GeneratedDocsPath = "UnityCsReference/_site";

        /// <summary>
        /// Gets the URL of the online API documentation of Unity.
        /// </summary>
        private const string UnityApiUrl = "https://docs.unity3d.com/ScriptReference/";

        /// <summary>
        /// The path of the Unity's csproj.
        /// </summary>
        private static readonly string[] UnityCsprojPaths = new []
        {
            "Projects/CSharp/UnityEditor.csproj",
            "Projects/CSharp/UnityEngine.csproj"
        };

        /// <summary>
        /// The path of the Unity repository.
        /// </summary>
        private const string UnityRepoPath = "UnityCsReference";

        /// <summary>
        /// The URL of the Unity repository.
        /// </summary>
        private const string UnityRepoUrl = "https://github.com/Unity-Technologies/UnityCsReference.git";

        /// <summary>
        /// The xref map filename.
        /// </summary>
        private const string XrefMapFileName = "xrefmap.yml";

        /// <summary>
        /// The path where to copy the xref maps.
        /// </summary>
        private const string XrefMapsPath = "../_site/Unity";

        /// <summary>
        /// Entry point of this program.
        /// </summary>
        public static void Main()
        {
            using (var unityRepo = Git.GetSyncRepository(UnityRepoUrl, UnityRepoPath))
            {
                var versions = GetLatestVersions(unityRepo);
                var latestVersion = versions
                    .OrderByDescending(version => version.name)
                    .First(version => version.release.Contains('f'));

                foreach (var version in versions)
                {
                    string filePath = Path.Combine(GeneratedDocsPath, XrefMapFileName);
                    string copyPath = Path.Combine(XrefMapsPath, version.name, XrefMapFileName); // ./<version>/xrefmap.yml
                    string apiUrl = GetUnityApiUrl(version.name);

                    Console.WriteLine($"Generating Unity {version.name} xref map to '{copyPath}'");
                    GenerateXrefMap(unityRepo, version.release);
                    Utils.CopyFile(filePath, copyPath);

                    Console.WriteLine($"Fixing hrefs in '{copyPath}'");
                    var xrefMap = XrefMap.Load(copyPath);
                    xrefMap.FixHrefs(apiUrl);
                    xrefMap.Save(copyPath);

                    // Set the last version's xref map as the default one
                    if (version == latestVersion)
                    {
                        string rootPath = Path.Combine(XrefMapsPath, XrefMapFileName); // ./xrefmap.yml

                        Console.WriteLine($"Copy '{copyPath}' to '{rootPath}'");
                        Utils.CopyFile(filePath, rootPath);

                        xrefMap = XrefMap.Load(rootPath);
                        xrefMap.FixHrefs(UnityApiUrl);
                        xrefMap.Save(rootPath);
                    }

                    Console.WriteLine("\n");
                }
            }
        }

        /// <summary>
        /// Fix the specified csproj to be able to generate its metadata with DocFX.
        /// </summary>
        /// <param name="csprojPath">The path of the csproj.</param>
        private static void FixCsprojForDocFx(string csprojPath)
        {
            string text = File.ReadAllText(csprojPath);
            text = text.Replace("ItemGroup Condition=\" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' \"", "ItemGroup");
            File.WriteAllText(csprojPath, text);
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
            Console.WriteLine($"Hard reset '{repository.Info.WorkingDirectory}' to '{commit}'");
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
            
            // Fix the csproj
            Console.WriteLine($"Fixing the csproj of '{commit}'");
            foreach (string csprojFilePath in UnityCsprojPaths)
            {
                string fullFilePath = Path.Combine(UnityRepoPath, csprojFilePath);
                FixCsprojForDocFx(fullFilePath);
            }

            // Generate site and xref map
            Console.WriteLine($"Running DocFX on '{commit}'");
            Utils.RunCommand("docfx", output => Console.WriteLine(output), error => Console.WriteLine(error));
        }

        /// <summary>
        /// Returns a collection of the latest versions of a specified repository of Unity.
        /// </summary>
        /// <param name="unityRepository">The repository of Unity to use.</param>
        /// <returns>The latest versions.</returns>
        private static IEnumerable<(string name, string release)> GetLatestVersions(Repository unityRepository)
        {
            return Git.GetTags(unityRepository)
                .Select(release => (name: Regex.Match(release, @"\d{4}\.\d").Value, release))
                .GroupBy(version => version.name)
                .Select(version => version.First());
        }

        /// <summary>
        /// Gets the URL of the online API documentation of Unity.
        /// </summary>
        /// <param name="version">The version of the API to use. If `null`, uses latest version.</param>
        /// <returns>The URL of the API corresponding to <paramref name="version"/>.</returns>
        private static string GetUnityApiUrl(string version)
        {
            return $"https://docs.unity3d.com/{version}/Documentation/ScriptReference/";
        }
    }
}
