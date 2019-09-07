using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NormandErwan.DocFxForUnity
{
    /// <summary>
    /// Represents a <see cref="XrefMapReference"/> from a Unity's <see cref="XrefMap"/>.
    /// </summary>
    public class UnityXrefMapReference : XrefMapReference
    {
        /// <summary>
        /// URL of the online API documentation of Unity.
        /// </summary>
        private const string UnityApiUrl = "https://docs.unity3d.com/ScriptReference/";

        /// <summary>
        /// The online API documentation of Unity doesn't show some namespaces.
        /// </summary>
        private static readonly List<string> HrefNamespacesToTrim = new List<string> { "UnityEditor", "UnityEngine" };

        /// <summary>
        /// Set <see cref="XrefMapReference.href"/> to link to the online API documentation of Unity.
        /// </summary>
        /// <param name="apiUrl">The URL of the online API documentation of Unity.</param>
        /// <returns>If this reference is valid or not.</returns>
        public bool TryFixHref(string apiUrl = UnityApiUrl)
        {
            string href;

            // Remove overloads
            if (commentId.Contains("Overload:"))
            {
                return false;
            }

            // Namespaces point to documentation index
            if (commentId.Contains("N:"))
            {
                href = "index";
            }
            else
            {
                href = uid;

                // Trim UnityEngine and UnityEditor namespaces from href
                foreach (var hrefNamespaceToTrim in HrefNamespacesToTrim)
                {
                    href = href.Replace(hrefNamespaceToTrim + ".", "");
                }

                // Fix href of constructors
                href = href.Replace(".#ctor", "-ctor");

                // Fix href of generics
                href = Regex.Replace(href, @"`{2}\d", "");
                href = href.Replace("`", "_");

                // Fix href of methods
                href = Regex.Replace(href, @"\*$", "");
                href = Regex.Replace(href, @"\(.*\)", "");

                // Fix href of properties
                if (commentId.Contains("P:") || commentId.Contains("M:"))
                {
                    href = Regex.Replace(href, @"\.([a-z].*)$", "-$1");
                }
            }

            this.href = apiUrl + href + ".html";
            return true;
        }
    }
}