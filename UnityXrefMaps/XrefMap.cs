using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace DocFxForUnity
{
    /// <summary>
    /// Represents a xref map file of Unity.
    /// </summary>
    public sealed class XrefMap
    {
        private static readonly Deserializer Deserializer = new Deserializer();

        private static readonly Serializer Serializer =
            Serializer.FromValueSerializer(
                new SerializerBuilder()
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                    .BuildValueSerializer(),
                EmitterSettings.Default
            );

        public bool sorted { get; set; }

        public XrefMapReference[] references { get; set; }

        /// <summary>
        /// Loads a <see cref="XrefMap"/> from a file.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>The loaded <see cref="XrefMap"/> from <paramref name="filePath"/>.</returns>
        public static XrefMap Load(string filePath)
        {
            string xrefMapText = File.ReadAllText(filePath);

            // Remove `0:` strings on the xrefmap that make crash Deserializer
            xrefMapText = Regex.Replace(xrefMapText, @"(\d):", "$1");

            return Deserializer.Deserialize<XrefMap>(xrefMapText);
        }

        /// <summary>
        /// Fix the <see cref="XrefMapReference.href"/> of <see cref="references"/> of this <see cref="XrefMap"/>.
        /// </summary>
        /// <param name="apiUrl">The URL of the online API documentation of Unity.</param>
        public void FixHrefs(string apiUrl, bool testUrls = false)
        {
            var fixedReferences = new List<XrefMapReference>();
            foreach (var reference in references)
            {
                if (!reference.IsValid)
                {
                    continue;
                }

                reference.FixOverloadCommentId(references);
                reference.FixHref(apiUrl);
                fixedReferences.Add(reference);
            }
            references = fixedReferences.ToArray();

            if (testUrls) {
                Console.WriteLine($"Testing URLs for {references.Length} references");

                fixedReferences.Clear();
                foreach (var reference in references) {
                    Task<bool> testTask = Utils.TestUriExists(reference.href);
                    testTask.Wait();
                    if (testTask.Result) {
                        fixedReferences.Add(reference);
                    }
                    else {
                        Console.WriteLine("Warning: invalid URL " + reference.href + " for uid " + reference.uid);
                    }
                }

                Console.WriteLine($"Removed {references.Length - fixedReferences.Count} invalid URLs");
                references = fixedReferences.ToArray();
            }
        }

        /// <summary>
        /// Saves this <see cref="XrefMap"/> to a file.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        public void Save(string filePath)
        {
            string xrefMapText = "### YamlMime:XRefMap\n"
                + Serializer.Serialize(this);
            File.WriteAllText(filePath, xrefMapText);
        }
    }
}