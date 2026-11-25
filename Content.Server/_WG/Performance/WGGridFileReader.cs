using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;

namespace Content.Server._WG.Performance;

/// <summary>
/// Reads and parses a grid file on the thread pool, then loads it on the main thread, so a tick pays for the entities
/// and not the YAML. <c>MapLoaderSystem.TryLoadGrid</c> does both on the main thread at once.
/// </summary>
public static class WGGridFileReader
{
    /// <summary>Reads and parses a map file off the main thread; null if it is missing or isn't one document.</summary>
    public static Task<MappingDataNode?> ReadAsync(IResourceManager resources, ResPath path)
    {
        var rooted = path.ToRootedPath();
        return Task.Run(() =>
        {
            TextReader reader;
            if (resources.UserData.Exists(rooted))
                reader = resources.UserData.OpenText(rooted);
            else if (resources.TryContentFileRead(rooted, out var stream))
                reader = new StreamReader(stream);
            else
                return null;

            using (reader)
            {
                var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
                return documents.Length == 1 ? (MappingDataNode?) documents[0].Root : null;
            }
        });
    }

    /// <summary>Reads and parses saved YAML text off the main thread; null if it isn't one document.</summary>
    public static Task<MappingDataNode?> ParseAsync(Func<TextReader> open)
    {
        return Task.Run(() =>
        {
            using var reader = open();
            var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
            return documents.Length == 1 ? (MappingDataNode?) documents[0].Root : null;
        });
    }

    /// <summary>Loads a parsed single-grid file onto a map, as <c>TryLoadGrid</c> loads the file itself.</summary>
    public static bool TryLoad(MapLoaderSystem loader, MappingDataNode data, ResPath path, MapId map,
        [NotNullWhen(true)] out Entity<MapGridComponent>? grid, System.Numerics.Vector2 offset = default)
    {
        return TryLoad(loader, data, path.ToString(), map, out grid, offset);
    }

    /// <summary>Loads parsed single-grid data onto a map; <paramref name="source"/> names it in errors.</summary>
    public static bool TryLoad(MapLoaderSystem loader, MappingDataNode data, string source, MapId map,
        [NotNullWhen(true)] out Entity<MapGridComponent>? grid, System.Numerics.Vector2 offset = default)
    {
        grid = null;
        var options = new MapLoadOptions { MergeMap = map, Offset = offset, ExpectedCategory = FileCategory.Grid };
        if (!loader.TryLoadGeneric(data, source, out var result, options))
            return false;

        if (result.Grids.Count != 1)
        {
            loader.Delete(result);
            return false;
        }

        grid = result.Grids.Single();
        return true;
    }
}
