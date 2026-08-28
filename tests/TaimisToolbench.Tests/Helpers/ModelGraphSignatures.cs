using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace TaimisToolbench.Tests.Helpers
{
    /// <summary>
    /// "Type.Property:PropertyType" for every Models-namespace class
    /// reachable from a root, plus the snapshot plumbing the on-disk
    /// schema guards share. Extracted from
    /// PersistedPlanSchemaMemberSetTests when the plan history index grew
    /// a guard of its own: two independently maintained graph walkers
    /// would have drifted, and the one that drifted would have been the
    /// one nobody was watching.
    /// <para>
    /// The walk unwraps List&lt;T&gt;, arrays, Nullable&lt;T&gt; and
    /// dictionaries, so a rename, addition, removal OR retype anywhere in
    /// a persisted graph moves the signature list - not only on the types
    /// a doc comment happened to name. Describe recurses into generic
    /// arguments because Type.Name alone reports List`1 for both
    /// List&lt;CurrencyCost&gt; and List&lt;ItemMetadata&gt;, which made a
    /// retype invisible whenever both element types were reachable
    /// elsewhere in the same snapshot.
    /// </para>
    /// </summary>
    internal static class ModelGraphSignatures
    {
        private const string ModelsNamespace = "TaimisToolbench.Models";

        public static string[] For(Type root)
        {
            return ReachableModelTypes(root)
                .SelectMany(MemberSignatures)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool ShouldUpdateSnapshots()
        {
            return Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";
        }

        public static string[] ReadSnapshot(string relativePath)
        {
            return File.ReadAllLines(SnapshotPath(relativePath))
                .Where(line => line.Length > 0)
                .ToArray();
        }

        public static void WriteSnapshot(string relativePath, string[] signatures)
        {
            File.WriteAllText(SnapshotPath(relativePath), string.Join("\n", signatures) + "\n");
        }

        public static string Sha256(string value)
        {
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var text = new StringBuilder(digest.Length * 2);
                foreach (byte b in digest)
                {
                    text.Append(b.ToString("x2"));
                }

                return text.ToString();
            }
        }

        private static string SnapshotPath(string relativePath)
        {
            string path = RepoFileLocator.FindRepoFile(
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(path))
            {
                throw new FileNotFoundException(
                    "Could not locate " + relativePath
                    + " by walking up from the test assembly's directory.");
            }

            return path;
        }

        private static IReadOnlyCollection<Type> ReachableModelTypes(Type root)
        {
            var visited = new HashSet<Type>();
            var queue = new Queue<Type>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Type type = queue.Dequeue();
                if (!visited.Add(type))
                {
                    continue;
                }

                foreach (PropertyInfo property in DeclaredProperties(type))
                {
                    foreach (Type candidate in UnwrapModelTypes(property.PropertyType))
                    {
                        queue.Enqueue(candidate);
                    }
                }
            }

            return visited;
        }

        private static IEnumerable<Type> UnwrapModelTypes(Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                type = underlying;
            }

            if (type.IsArray)
            {
                foreach (Type inner in UnwrapModelTypes(type.GetElementType()))
                {
                    yield return inner;
                }

                yield break;
            }

            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    foreach (Type inner in UnwrapModelTypes(argument))
                    {
                        yield return inner;
                    }
                }

                yield break;
            }

            if (type.IsClass && type.Namespace == ModelsNamespace)
            {
                yield return type;
            }
        }

        private static IEnumerable<PropertyInfo> DeclaredProperties(Type type)
        {
            return type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        }

        private static IEnumerable<string> MemberSignatures(Type type)
        {
            return DeclaredProperties(type)
                .Select(p => type.Name + "." + p.Name + ":" + Describe(p.PropertyType));
        }

        private static string Describe(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            string args = string.Join(",", type.GetGenericArguments().Select(Describe));
            return type.Name + "<" + args + ">";
        }
    }
}
