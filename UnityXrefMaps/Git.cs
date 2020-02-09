using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;

namespace DocFxForUnity
{
    public sealed class Git
    {
        /// <summary>
        /// Fetches changes and hard resets the specified repository to the latest commit of a specified branch. If no
        /// repository is found, it will be cloned before.
        /// </summary>
        /// <param name="sourceUrl">The url of the repository.</param>
        /// <param name="path">The directory path where to find/clone the repository.</param>
        /// <param name="branch">The branch use on the repository.</param>
        /// <returns>The synced repository on the latest commit of the specified branch.</returns>
        public static Repository GetSyncRepository(string sourceUrl, string path, string branch = "master")
        {
            // Clone this repository to the specified branch if it doesn't exist
            bool clone = !Directory.Exists(path);
            if (clone)
            {
                Console.WriteLine($"Clonning {sourceUrl} to {path}");

                var options = new CloneOptions() { BranchName = branch };
                Repository.Clone(sourceUrl, path, options);
            }

            var repository = new Repository(path);

            // Otherwise fetch changes and checkout to the specified branch
            if (!clone)
            {
                Console.WriteLine($"Hard reset '{path}' to HEAD");
                repository.Reset(ResetMode.Hard);
                repository.RemoveUntrackedFiles();

                Console.WriteLine($"Fetching changes from 'origin' in '{path}'");
                var remote = repository.Network.Remotes["origin"];
                Commands.Fetch(repository, remote.Name, new string[0], null, null); // WTF is this API libgit2sharp?

                Console.WriteLine($"Checking out '{path}' to '{branch}' branch");
                var remoteBranch = $"origin/{branch}";
                Commands.Checkout(repository, remoteBranch);
            }

            Console.WriteLine();

            return repository;
        }

        /// <summary>
        /// Returns a collection of the latest tags of a specified repository.
        /// </summary>
        /// <param name="repository">The repository to use.</param>
        /// <returns>The collection of tags.</returns>
        public static IEnumerable<string> GetTags(Repository repository)
        {
            return repository.Tags
                .OrderByDescending(tag => (tag.Target as Commit).Author.When)
                .Select(tag => tag.FriendlyName);
        }
    }
}