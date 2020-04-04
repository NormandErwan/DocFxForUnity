using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace DocFxForUnity
{
    /// <summary>
    /// Represents a xref map file of Unity.
    /// </summary>
    public sealed class XrefMap
    {
        private static readonly Deserializer Deserializer = new Deserializer();
        private static readonly Serializer Serializer = new Serializer();

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

                reference.FixHref(apiUrl);
                fixedReferences.Add(reference);

                if (testUrls && !Utils.TestUriExists(reference.href).Result)
                {
                    Console.WriteLine("Warning: invalid URL " + reference.href + " for " + reference.uid + " uid");
                }
            }
            references = fixedReferences.ToArray();
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