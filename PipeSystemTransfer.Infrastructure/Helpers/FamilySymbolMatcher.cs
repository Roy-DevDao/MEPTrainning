using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace PipeSystemTransfer.Infrastructure.Helpers
{
    internal static class FamilySymbolMatcher
    {
        private static readonly char[] SplitChars = { ' ', '-', '_', '/', '°', '(', ')' };

        private static readonly HashSet<string> TypeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tee", "elbow", "union", "transition", "cap", "wye", "flange", "trap",
            "siphon", "coupling", "joint", "reducer", "bend", "cross", "plug",
            "socket", "sweep", "swept", "lateral", "saddle", "valve", "adapter",
            "inspection", "cleanout", "strainer", "nipple", "bushing"
        };

        private static readonly Dictionary<string, string> AbbrevMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "flg",  "flange"     }, { "elb",  "elbow"      }, { "ell",  "elbow"      },
            { "red",  "reducer"    }, { "thr",  "thread"     }, { "thrd", "thread"     },
            { "cplg", "coupling"   }, { "str",  "strainer"   }, { "adpt", "adapter"    },
            { "trans","transition" }, { "unin", "union"      }, { "flex", "flexible"   }
        };

        private enum GenericShape { Unknown, Elbow, Tee, Cross, Transition, Cap, Coupling }

        internal static FamilySymbol FindBestMatch(
            Dictionary<(string, string), FamilySymbol> cache,
            string familyName, string typeName)
        {
            if (cache.Count == 0) return null;

            var shape  = DetectGenericShape(familyName, typeName);
            var mapped = TryMapToGenericFamily(cache, shape);
            if (mapped != null) return mapped;

            var rawWords   = $"{familyName} {typeName}".ToLowerInvariant()
                              .Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            var queryWords = new HashSet<string>();
            foreach (var w in rawWords)
            {
                queryWords.Add(w);
                if (AbbrevMap.TryGetValue(w, out var expanded))
                    queryWords.Add(expanded);
            }

            FamilySymbol best     = null;
            int          bestScore = 0;
            foreach (var kvp in cache)
            {
                var candidate = $"{kvp.Key.Item1} {kvp.Key.Item2}".ToLowerInvariant();
                int score = 0;
                foreach (var word in queryWords)
                    if (candidate.Contains(word))
                        score += TypeKeywords.Contains(word) ? 10 : 1;
                if (score > bestScore) { bestScore = score; best = kvp.Value; }
            }

            return best ?? cache.Values.FirstOrDefault();
        }

        private static GenericShape DetectGenericShape(string familyName, string typeName)
        {
            var name = $"{familyName} {typeName}".ToLowerInvariant();
            if (name.Contains("tee"))                                        return GenericShape.Tee;
            if (name.Contains("elbow") || name.Contains("bend"))             return GenericShape.Elbow;
            if (name.Contains("cross"))                                      return GenericShape.Cross;
            if (name.Contains("transition") || name.Contains("trans"))       return GenericShape.Transition;
            if (name.Contains("cap"))                                        return GenericShape.Cap;
            if (name.Contains("coupling") || name.Contains("cplg"))         return GenericShape.Coupling;
            return GenericShape.Unknown;
        }

        private static FamilySymbol TryMapToGenericFamily(
            Dictionary<(string, string), FamilySymbol> cache, GenericShape shape)
        {
            if (shape == GenericShape.Unknown || cache.Count == 0) return null;

            string keyword;
            switch (shape)
            {
                case GenericShape.Elbow:      keyword = "elbow_generic";      break;
                case GenericShape.Tee:        keyword = "tee_generic";        break;
                case GenericShape.Cross:      keyword = "cross_generic";      break;
                case GenericShape.Transition: keyword = "transition_generic"; break;
                case GenericShape.Cap:        keyword = "cap_generic";        break;
                case GenericShape.Coupling:   keyword = "coupling_generic";   break;
                default: return null;
            }

            FamilySymbol best = null;
            foreach (var kvp in cache)
            {
                if (kvp.Key.Item1.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (kvp.Key.Item2.Equals("Standard", StringComparison.OrdinalIgnoreCase)) return kvp.Value;
                best = kvp.Value;
            }
            return best;
        }
    }
}
