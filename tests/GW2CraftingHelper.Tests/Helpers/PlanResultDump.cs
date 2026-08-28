using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using TaimisToolbench.Models;

namespace TaimisToolbench.Tests.Helpers
{
    /// <summary>
    /// A whole <see cref="CraftingPlanResult"/> rendered as deterministic
    /// text, so a golden file can hold every decision the solver made -
    /// chosen source per node, unit and total costs, craft step ordering,
    /// required recipes, shopping list, and every advisory list - rather
    /// than only the handful of fields a hand-written assertion picks.
    ///
    /// <para>
    /// Reflection, deliberately: a field added to a model that the solver
    /// starts populating changes the dump, and the golden catches it. A
    /// hand-listed serializer would silently keep passing.
    /// </para>
    ///
    /// <para>
    /// Everything is ordered by name (properties) or by key (dictionaries),
    /// and every number is invariant-culture, so two runs on two machines
    /// produce identical bytes.
    /// </para>
    /// </summary>
    internal static class PlanResultDump
    {
        /// <summary>
        /// Members whose value is legitimately not reproducible run to run.
        /// DebugLog carries stopwatch timings.
        /// </summary>
        private static readonly HashSet<string> SkippedMembers = new HashSet<string>
        {
            nameof(CraftingPlanResult.DebugLog),
        };

        private const int MaxDepth = 40;

        public static string Render(CraftingPlanResult result)
        {
            var sb = new StringBuilder(8192);
            Write(sb, "result", result, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, string path, object value, int depth, HashSet<object> seen)
        {
            if (depth > MaxDepth)
            {
                sb.Append(path).Append(" = <depth cap>\n");
                return;
            }

            if (value == null)
            {
                sb.Append(path).Append(" = null\n");
                return;
            }

            var type = value.GetType();

            if (IsScalar(type))
            {
                sb.Append(path).Append(" = ").Append(Scalar(value)).Append('\n');
                return;
            }

            if (!seen.Add(value))
            {
                // A back-reference (SolveContext threads the request items
                // that the result also holds). Named, not followed.
                sb.Append(path).Append(" = <already dumped>\n");
                return;
            }

            if (value is IDictionary dictionary)
            {
                var keys = dictionary.Keys.Cast<object>()
                    .OrderBy(Scalar, StringComparer.Ordinal)
                    .ToList();
                sb.Append(path).Append(".count = ").Append(keys.Count).Append('\n');
                foreach (var key in keys)
                {
                    Write(sb, path + "[" + Scalar(key) + "]", dictionary[key], depth + 1, seen);
                }

                return;
            }

            if (value is IEnumerable enumerable)
            {
                var items = enumerable.Cast<object>().ToList();

                // A set has no order of its own, so one is imposed - two
                // runs must not differ on hash iteration order.
                if (IsSet(type))
                {
                    items = items.OrderBy(Scalar, StringComparer.Ordinal).ToList();
                }

                sb.Append(path).Append(".count = ").Append(items.Count).Append('\n');
                for (int i = 0; i < items.Count; i++)
                {
                    Write(sb, path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", items[i], depth + 1, seen);
                }

                return;
            }

            foreach (var property in type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (SkippedMembers.Contains(property.Name))
                {
                    sb.Append(path).Append('.').Append(property.Name).Append(" = <skipped>\n");
                    continue;
                }

                object propertyValue;
                try
                {
                    propertyValue = property.GetValue(value);
                }
                catch (TargetInvocationException ex)
                {
                    propertyValue = "<threw " + ex.InnerException?.GetType().Name + ">";
                }

                Write(sb, path + "." + property.Name, propertyValue, depth + 1, seen);
            }
        }

        private static bool IsScalar(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan)
                || type == typeof(Guid);
        }

        private static bool IsSet(Type type)
        {
            return type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>));
        }

        private static string Scalar(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        /// <summary>
        /// Cycle detection must be by identity: two equal-by-value model
        /// records are still two nodes of the tree and both must be dumped.
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            bool IEqualityComparer<object>.Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
