using System;
using LibGit2Sharp;

namespace UnityDocs
{
    class Program
    {
        private const string UnityRepoPath = "Unity";

        static void Main(string[] args)
        {
            using (var repo = new Repository(UnityRepoPath))
            {
                foreach (var tag in repo.Tags)
                {
                    Console.WriteLine(tag.FriendlyName);
                }
            }
        }
    }
}
