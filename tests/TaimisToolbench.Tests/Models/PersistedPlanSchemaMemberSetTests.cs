using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TaimisToolbench.Models;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Models
{
    // B1 (quality-phase1-bugs, quality-audit follow-up): the original guard
    // only snapshotted 4 hand-picked types (PersistedPlan/CraftingPlanResult/
    // PlanSolveContext/CraftingTreeNode) and missed every other type reachable
    // through them - RequiredRecipe, VendorOffer, PillSourceCostBreakdown, and
    // several more all grew public properties after the 1 -> 2 bump without
    // this test ever noticing. This version instead walks the full object
    // graph reachable from PersistedPlan (unwrapping List<T>/array/Nullable<T>/
    // IReadOnlyDictionary<K,V> etc.) and snapshots "Type.Property:PropertyType"
    // for every reachable Models-namespace class, so a rename, addition,
    // removal, OR retype anywhere in the persisted graph fails this test - not
    // just on the four types named in CurrentSchemaVersion's doc comment. See
    // KNOWN-ISSUES #53 for the full quality-audit rationale.
    //
    // The signature list itself lives in tests/shared/persisted_plan_schema.txt
    // rather than in a C# array literal, so a shape change shows up as a
    // readable text diff; UPDATE_SNAPSHOTS=1 regenerates it. Its SHA-256 is
    // stored on PersistedPlan next to CurrentSchemaVersion, which is what
    // couples the two - see PersistedPlan.SchemaShapeHash.
    public class PersistedPlanSchemaMemberSetTests
    {
        private const string ModelsNamespace = "TaimisToolbench.Models";

        private const string SnapshotRelativePath = "tests/shared/persisted_plan_schema.txt";

        [Fact]
        public void CurrentSchemaVersion_MatchesExpectedValue()
        {
            Assert.Equal(3, PersistedPlan.CurrentSchemaVersion);
        }

        [Fact]
        public void PersistedPlanGraph_PublicMemberSignature_MatchesSnapshot()
        {
            string[] actual = CurrentSignatures();
            string[] expected = ReadSnapshot();

            if (ShouldUpdateSnapshots())
            {
                WriteSnapshot(actual);
                expected = actual;
            }

            // Line-by-line text, so the reviewer's diff names the property
            // that moved instead of "collections differ at index 137".
            Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
        }

        [Fact]
        public void PersistedPlanGraph_ShapeHash_MatchesTheOneStoredBesideTheVersion()
        {
            // The coupling the pair was always described as having and did
            // not: CurrentSchemaVersion_MatchesExpectedValue and the
            // snapshot test are independent, so editing the snapshot alone
            // used to make a shape change green with the version untouched.
            // The hash lives on PersistedPlan, one line from
            // CurrentSchemaVersion, so a graph change cannot be absorbed
            // without editing the version's own neighbourhood.
            string actualHash = Sha256(string.Join("\n", CurrentSignatures()));

            Assert.True(
                actualHash == PersistedPlan.SchemaShapeHash,
                "The persisted plan graph's shape changed.\n"
                + "  expected (PersistedPlan.SchemaShapeHash): " + PersistedPlan.SchemaShapeHash + "\n"
                + "  actual:                                   " + actualHash + "\n"
                + "Run the suite with UPDATE_SNAPSHOTS=1 to rewrite "
                + SnapshotRelativePath + ", review that text diff, then set "
                + "SchemaShapeHash to the actual value above and decide "
                + "whether PersistedPlan.CurrentSchemaVersion must be bumped "
                + "(it must, for any rename, removal or retype).");
        }

        private static string[] CurrentSignatures()
        {
            return ReachableModelTypes(typeof(PersistedPlan))
                .SelectMany(MemberSignatures)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool ShouldUpdateSnapshots()
        {
            return Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";
        }

        private static string SnapshotPath()
        {
            string path = RepoFileLocator.FindRepoFile(
                Path.Combine("tests", "shared", "persisted_plan_schema.txt"));
            if (string.IsNullOrEmpty(path))
            {
                throw new FileNotFoundException(
                    "Could not locate " + SnapshotRelativePath
                    + " by walking up from the test assembly's directory.");
            }

            return path;
        }

        private static string[] ReadSnapshot()
        {
            return File.ReadAllLines(SnapshotPath())
                .Where(line => line.Length > 0)
                .ToArray();
        }

        private static void WriteSnapshot(string[] signatures)
        {
            File.WriteAllText(SnapshotPath(), string.Join("\n", signatures) + "\n");
        }

        private static string Sha256(string value)
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

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
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

        private static IEnumerable<string> MemberSignatures(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => type.Name + "." + p.Name + ":" + Describe(p.PropertyType));
        }

        // Retype-blind-spot fix (quality-phase1-bugs): Type.Name alone drops
        // generic arguments (List<CurrencyCost> and List<ItemMetadata> both
        // report "List`1"), so retyping an element/key/value type anywhere
        // reachable in the graph was silently invisible whenever both the
        // old and new element types were themselves reachable elsewhere in
        // the same snapshot. Describe recurses into generic arguments so
        // the signature captures the full shape (e.g. "List`1<CurrencyCost>",
        // "Nullable`1<Int64>").
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
