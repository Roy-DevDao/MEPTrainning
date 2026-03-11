using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeSystemTransfer.Infrastructure.Helpers
{
    internal class RevitElementCache
    {
        private readonly Document _doc;

        internal RevitElementCache(Document doc) => _doc = doc;

        internal Dictionary<(string, string), FamilySymbol> BuildSymbolCache()
            => BuildSymbolCacheInternal(null);

        internal Dictionary<(string, string), FamilySymbol> BuildFittingSymbolCache()
            => BuildSymbolCacheInternal(BuiltInCategory.OST_PipeFitting);

        internal Dictionary<string, PipeType> BuildPipeTypeMap()
            => Collect<PipeType>().ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, Level> BuildLevelMap()
            => Collect<Level>().ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, PipingSystemType> BuildPipeSystemTypeMap()
            => Collect<PipingSystemType>().ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);

        private Dictionary<(string, string), FamilySymbol> BuildSymbolCacheInternal(BuiltInCategory? category)
        {
            var cache     = new Dictionary<(string, string), FamilySymbol>();
            var collector = new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol));
            if (category.HasValue)
                collector = collector.OfCategory(category.Value);

            foreach (var s in collector.Cast<FamilySymbol>())
            {
                var key = (s.FamilyName, s.Name);
                if (!cache.ContainsKey(key))
                    cache[key] = s;
            }
            return cache;
        }

        private IEnumerable<T> Collect<T>() where T : Element
            => new FilteredElementCollector(_doc).OfClass(typeof(T)).Cast<T>();
    }
}
