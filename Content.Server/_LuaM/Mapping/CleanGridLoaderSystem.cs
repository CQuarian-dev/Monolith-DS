using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Holiday;
using Content.Server.Maps;
using Content.Shared.Decals;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Mapping;

public sealed class CleanGridLoaderSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const string Placeholder = "LuaMCleanLoadPlaceholder";

    public const string FallbackTile = "Plating";

    private readonly HashSet<string> _pendingMissing = new();

    private readonly HashSet<string> _substituted = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeforeEntityReadEvent>(OnBeforeEntityRead,
            after: new[] { typeof(MapMigrationSystem), typeof(HolidaySystem) });
    }

    private void OnBeforeEntityRead(BeforeEntityReadEvent ev)
    {
        foreach (var id in _pendingMissing)
        {
            if (ev.DeletedPrototypes.Contains(id) || ev.RenamedPrototypes.ContainsKey(id))
                continue;

            if (ev.RenamedPrototypes.TryAdd(id, Placeholder))
                _substituted.Add(id);
        }
    }

    public bool TryLoadGrid(
        MapId map,
        ResPath path,
        DeserializationOptions options,
        Vector2 offset,
        Angle rotation,
        [NotNullWhen(true)] out Entity<MapGridComponent>? grid,
        out CleanLoadReport report,
        out string? error)
    {
        grid = null;
        report = new CleanLoadReport();
        error = null;

        if (!_tileDefs.TryGetDefinition(FallbackTile, out _))
        {
            error = "tile";
            return false;
        }

        if (!_mapLoader.TryReadFile(path, out var data))
        {
            error = "file";
            return false;
        }

        Dictionary<string, int> missing;
        try
        {
            var version = data.Get<MappingDataNode>("meta").Get<ValueDataNode>("format").AsInt();
            missing = CollectMissingPrototypes(data, version);
            ReplaceMissingTiles(data, report);
            RemoveMissingDecals(data, version, report);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read entities of {path}: {e}");
            error = "format";
            return false;
        }

        var opts = new MapLoadOptions
        {
            MergeMap = map,
            Offset = offset,
            Rotation = rotation,
            DeserializationOptions = options,
            ExpectedCategory = FileCategory.Grid,
        };

        LoadResult? result;
        _substituted.Clear();
        _pendingMissing.UnionWith(missing.Keys);
        try
        {
            if (!_mapLoader.TryLoadGeneric(data, path.ToString(), out result, opts))
            {
                error = "load";
                return false;
            }
        }
        finally
        {
            _pendingMissing.Clear();
        }

        foreach (var id in _substituted)
        {
            report.Missing[id] = missing[id];
        }
        _substituted.Clear();

        if (result.Grids.Count != 1)
        {
            _mapLoader.Delete(result);
            error = "grids";
            return false;
        }

        ReplacePlaceholders(result, report);
        grid = result.Grids.Single();
        return true;
    }

    private Dictionary<string, int> CollectMissingPrototypes(MappingDataNode data, int version)
    {
        var missing = new Dictionary<string, int>();
        var key = version >= 4 ? "proto" : "type";

        foreach (var node in data.Get<SequenceDataNode>("entities").Cast<MappingDataNode>())
        {
            if (!node.TryGet<ValueDataNode>(key, out var protoNode) || string.IsNullOrWhiteSpace(protoNode.Value))
                continue;

            if (_proto.HasIndex<EntityPrototype>(protoNode.Value))
                continue;

            var count = version >= 4 && node.TryGet<SequenceDataNode>("entities", out var group) ? group.Count : 1;
            missing[protoNode.Value] = missing.GetValueOrDefault(protoNode.Value) + count;
        }

        return missing;
    }

    private void ReplaceMissingTiles(MappingDataNode data, CleanLoadReport report)
    {
        if (!data.TryGet<MappingDataNode>("tilemap", out var tileMap))
            return;

        var aliases = new Dictionary<string, string>();
        foreach (var alias in _proto.EnumeratePrototypes<TileAliasPrototype>())
        {
            aliases[alias.ID] = alias.Target;
        }

        foreach (var key in tileMap.Children.Keys.ToList())
        {
            if (tileMap[key] is not ValueDataNode value)
                continue;

            var resolved = aliases.GetValueOrDefault(value.Value, value.Value);
            if (_tileDefs.TryGetDefinition(resolved, out _))
                continue;

            report.MissingTiles.Add(value.Value);
            tileMap[key] = new ValueDataNode(FallbackTile);
        }
    }

    private void RemoveMissingDecals(MappingDataNode data, int version, CleanLoadReport report)
    {
        if (!data.TryGet<SequenceDataNode>("entities", out var entities))
            return;

        foreach (var entity in EnumerateEntityNodes(entities, version))
        {
            if (!entity.TryGet<SequenceDataNode>("components", out var components))
                continue;

            foreach (var component in components)
            {
                if (component is not MappingDataNode comp
                    || !comp.TryGet<ValueDataNode>("type", out var type)
                    || type.Value != "DecalGrid")
                    continue;

                if (!comp.TryGet<MappingDataNode>("chunkCollection", out var collection)
                    || !collection.TryGet<SequenceDataNode>("nodes", out var nodes))
                    continue;

                for (var i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i] is not MappingDataNode group
                        || !group.TryGet<MappingDataNode>("node", out var node)
                        || !node.TryGet<ValueDataNode>("id", out var id)
                        || _proto.HasIndex<DecalPrototype>(id.Value))
                        continue;

                    var count = group.TryGet<MappingDataNode>("decals", out var decals) ? decals.Children.Count : 0;
                    report.MissingDecals[id.Value] = report.MissingDecals.GetValueOrDefault(id.Value) + count;
                    nodes.RemoveAt(i);
                }
            }
        }
    }

    private static IEnumerable<MappingDataNode> EnumerateEntityNodes(SequenceDataNode entities, int version)
    {
        foreach (var node in entities)
        {
            if (node is not MappingDataNode entry)
                continue;

            if (version < 4)
            {
                yield return entry;
                continue;
            }

            if (!entry.TryGet<SequenceDataNode>("entities", out var group))
                continue;

            foreach (var ent in group)
            {
                if (ent is MappingDataNode mapped)
                    yield return mapped;
            }
        }
    }

    private void ReplacePlaceholders(LoadResult result, CleanLoadReport report)
    {
        var placeholders = new List<EntityUid>();
        foreach (var uid in result.Entities)
        {
            if (!TerminatingOrDeleted(uid) && MetaData(uid).EntityPrototype?.ID == Placeholder)
                placeholders.Add(uid);
        }

        var children = new List<EntityUid>();
        foreach (var uid in placeholders)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (TryComp<ContainerManagerComponent>(uid, out var manager))
            {
                foreach (var container in manager.Containers.Values.ToArray())
                {
                    foreach (var contained in _container.EmptyContainer(container, force: true))
                    {
                        _transform.DropNextTo(contained, uid);
                        report.Rescued++;
                    }
                }
            }

            children.Clear();
            var enumerator = Transform(uid).ChildEnumerator;
            while (enumerator.MoveNext(out var child))
            {
                children.Add(child);
            }

            foreach (var child in children)
            {
                _transform.DropNextTo(child, uid);
                report.Rescued++;
            }

            Del(uid);
            report.Removed++;
        }
    }
}

public sealed class CleanLoadReport
{
    public readonly Dictionary<string, int> Missing = new();

    public readonly HashSet<string> MissingTiles = new();

    public readonly Dictionary<string, int> MissingDecals = new();

    public int Removed;
    public int Rescued;
}
