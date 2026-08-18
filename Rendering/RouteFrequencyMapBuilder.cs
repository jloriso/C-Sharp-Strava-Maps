using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Builds the "well-worn route" line data for the line map, one FeatureCollection
/// per activity type.
///
/// Every activity's points are snapped to a small grid (collapsing consecutive
/// duplicate cells, which both smooths GPS jitter and identifies shared road/trail
/// segments across activities of the same type), and each grid edge is scored by
/// how many distinct activities of that type crossed it, bucketed into one of six
/// fixed frequency tiers (1x, 2-3x, 4-7x, 8-15x, 16-31x, 32x+).
///
/// Using a small, fixed set of tiers -- rather than a continuous weight/opacity
/// scaled to this dataset's own busiest route -- is what makes the next step
/// possible: consecutive edges that share the same tier are merged into a single
/// longer LineString wherever the route doesn't branch (a graph "path
/// contraction"). On a real activity history this collapses what would otherwise
/// be tens of thousands of tiny 2-point edges into a much smaller number of long
/// polylines. That reduction in *feature count* -- not the number of coordinates
/// within them -- is the single biggest lever for keeping the line map responsive:
/// every one of those tiny edges was its own separately clickable, separately
/// hit-tested Leaflet layer, and that per-feature overhead (not total data volume)
/// is what made panning/zooming/clicking feel sluggish.
///
/// Each rendered vertex is the *average* of the real GPS points that snapped into
/// that grid cell (see centroidSums/nodeCentroid below) rather than the cell's raw
/// mathematical corner. Using the grid corner directly would make merged lines
/// jump between artificial lat/lon-aligned intersections instead of following the
/// road's true path -- visible as a "staircase" look on any diagonal road, most
/// noticeable when zoomed in. Averaging real points keeps the smoothing/matching
/// benefit of the grid without that artifact.
/// </summary>
public static class RouteFrequencyMapBuilder
{
    // Distance (in meters) that consecutive points get snapped to before being
    // treated as graph nodes. This is the main lever for GPS noise: a consumer
    // GPS typically drifts 3-10m even riding the exact same road twice, so too
    // fine a grid causes two passes over the same physical route to land on
    // *different* nearby cells instead of the same one. That shows up as (a)
    // several near-parallel, slightly-offset lines criss-crossing each other once
    // you zoom in ("jumbled"), and (b) what should be one well-traveled edge
    // getting fragmented into several separate edges, each under-counted on its
    // own and therefore landing in a fainter tier than the route actually
    // deserves. Raising this value absorbs more of that jitter into the same
    // snapped point, so repeat routes line up cleanly *and* get counted
    // correctly. 25m clears typical GPS drift while still keeping separate
    // nearby streets distinct; push it higher (30-40m) if routes are still
    // misaligned, or lower it if separate close-together roads start visibly
    // merging into one line.
    const double GridSizeMeters = 25.0;

    // Index 0 = traveled once, 1 = 2-3x, 2 = 4-7x, 3 = 8-15x, 4 = 16-31x, 5 = 32x+.
    // The lowest tier's opacity/weight sets the floor for how visible a
    // once-traveled route is -- raised here (0.35 -> 0.5, 1.2 -> 1.5) so rare
    // routes stay clearly visible rather than nearly invisible, while still
    // reading as visually lighter than well-worn ones.
    static readonly double[] TierWeights = { 1.5, 2.2, 3.0, 4.0, 5.2, 6.5 };
    static readonly double[] TierOpacities = { 0.5, 0.6, 0.7, 0.82, 0.92, 1.0 };

    /// <summary>A grid-snapped point, used as a graph node when merging edges into
    /// chains.</summary>
    readonly record struct Node(long Lat, long Lon);

    /// <summary>A canonical (order-independent) key for the edge between two
    /// nodes, so the same physical edge always hashes the same way regardless of
    /// which direction it was travelled.</summary>
    readonly record struct EdgeKey(long Lat1, long Lon1, long Lat2, long Lon2);

    const string EmptyFeatureCollection = "{\"type\":\"FeatureCollection\",\"features\":[]}";

    public class Result
    {
        /// <summary>JS object literal: {"Run": {FeatureCollection...}, "Ride": {...}}.</summary>
        public string ByTypeJs = "{}";
    }

    public static Result Build(IEnumerable<ActivityTrack> tracks)
    {
        var sb = new StringBuilder("{");
        bool firstType = true;
        foreach (var group in tracks.GroupBy(t => ActivityCategory.CategoryForType(t.Type)))
        {
            if (!firstType) sb.Append(",");
            firstType = false;
            sb.Append(JsonStr(group.Key)).Append(":").Append(BuildTypeFeatureCollection(group.ToList()));
        }
        sb.Append("}");
        return new Result { ByTypeJs = sb.ToString() };
    }

    static string BuildTypeFeatureCollection(List<ActivityTrack> tracksOfType)
    {
        var allPoints = tracksOfType.SelectMany(t => t.AllPoints).ToList();
        if (allPoints.Count == 0) return EmptyFeatureCollection;

        double refLat = allPoints.Average(p => p.Lat);
        var (latStep, lonStep) = GridSteps(refLat);

        // Accumulates the real GPS points that land in each grid cell, so the
        // rendered line can follow their average position instead of the cell's
        // raw mathematical corner. A merged line built purely from grid math
        // jumps between artificial lat/lon-aligned intersections, which produces
        // a visible "staircase" on any road that runs diagonally to that grid --
        // averaging the actual points that fell into each cell keeps the line
        // following the road's true curve while still getting the GPS-jitter
        // smoothing and repeat-route matching that snapping to a grid provides.
        var centroidSums = new Dictionary<Node, (double SumLat, double SumLon, int Count)>();

        // 1. Count how many distinct activities crossed each grid edge.
        var edgeActivityIds = new Dictionary<EdgeKey, HashSet<string>>();
        foreach (var track in tracksOfType)
        {
            var seenEdgesThisActivity = new HashSet<EdgeKey>();
            foreach (var seg in track.Segments)
            {
                var cells = SnapAndCollapse(seg, latStep, lonStep, centroidSums);
                for (int i = 0; i + 1 < cells.Count; i++)
                {
                    var key = MakeEdgeKey(cells[i], cells[i + 1]);
                    if (!seenEdgesThisActivity.Add(key)) continue; // out-and-back shouldn't double-count itself

                    if (!edgeActivityIds.TryGetValue(key, out var ids))
                        edgeActivityIds[key] = ids = new HashSet<string>();
                    ids.Add(track.ActivityId);
                }
            }
        }
        if (edgeActivityIds.Count == 0) return EmptyFeatureCollection;

        // Collapse each cell's accumulated sums down to its average (lat, lon) --
        // this is the actual coordinate used when rendering, in place of the raw
        // grid corner.
        var nodeCentroid = centroidSums.ToDictionary(
            kv => kv.Key,
            kv => (Lat: kv.Value.SumLat / kv.Value.Count, Lon: kv.Value.SumLon / kv.Value.Count));

        // 2. Bucket each edge into one of six fixed frequency tiers.
        var edgeCount = edgeActivityIds.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
        var edgeTier = edgeCount.ToDictionary(kv => kv.Key, kv => TierForCount(kv.Value));

        // 3. Build an undirected node adjacency list so same-tier chains of edges
        //    can be walked and merged.
        var adjacency = new Dictionary<Node, List<(Node Neighbor, EdgeKey Key)>>();
        foreach (var key in edgeActivityIds.Keys)
        {
            var a = new Node(key.Lat1, key.Lon1);
            var b = new Node(key.Lat2, key.Lon2);
            AddAdjacency(adjacency, a, b, key);
            AddAdjacency(adjacency, b, a, key);
        }

        // 4. Walk and merge chains of same-tier, non-branching edges into single
        //    LineStrings, tracking the min/max crossing count actually observed
        //    along each merged chain -- tiers are bands, so this keeps the
        //    "traveled N times" popup accurate even after merging.
        var usedEdges = new HashSet<EdgeKey>();
        var features = new StringBuilder("[");
        bool firstFeature = true;

        foreach (var startKey in edgeActivityIds.Keys)
        {
            if (usedEdges.Contains(startKey)) continue;

            int tier = edgeTier[startKey];
            var a0 = new Node(startKey.Lat1, startKey.Lon1);
            var b0 = new Node(startKey.Lat2, startKey.Lon2);
            var chain = new List<Node> { a0, b0 };
            usedEdges.Add(startKey);
            int minCount = edgeCount[startKey], maxCount = minCount;

            ExtendChain(chain, forward: true, tier, adjacency, usedEdges, edgeCount, edgeTier, ref minCount, ref maxCount);
            ExtendChain(chain, forward: false, tier, adjacency, usedEdges, edgeCount, edgeTier, ref minCount, ref maxCount);

            if (!firstFeature) features.Append(",");
            firstFeature = false;
            features.Append(BuildChainFeature(chain, nodeCentroid, tier, minCount, maxCount));
        }
        features.Append("]");

        return "{\"type\":\"FeatureCollection\",\"features\":" + features + "}";
    }

    /// <summary>Extends a chain forward (appending) or backward (prepending)
    /// through unused same-tier edges, stopping at a dead end, a tier change, or a
    /// branch (a node with more than one same-tier unused edge to continue
    /// through) -- so merging never changes the route's actual shape or its
    /// frequency coloring, it only combines edges that would have rendered
    /// identically anyway.</summary>
    static void ExtendChain(
        List<Node> chain, bool forward, int tier,
        Dictionary<Node, List<(Node Neighbor, EdgeKey Key)>> adjacency,
        HashSet<EdgeKey> usedEdges,
        Dictionary<EdgeKey, int> edgeCount,
        Dictionary<EdgeKey, int> edgeTier,
        ref int minCount, ref int maxCount)
    {
        while (true)
        {
            var current = forward ? chain[^1] : chain[0];
            if (!adjacency.TryGetValue(current, out var neighbors)) return;

            var candidates = neighbors.Where(n => !usedEdges.Contains(n.Key) && edgeTier[n.Key] == tier).ToList();
            if (candidates.Count != 1) return; // dead end or branch -- stop merging here

            var (next, key) = candidates[0];
            usedEdges.Add(key);
            int count = edgeCount[key];
            minCount = Math.Min(minCount, count);
            maxCount = Math.Max(maxCount, count);

            if (forward) chain.Add(next); else chain.Insert(0, next);
        }
    }

    static string BuildChainFeature(
        List<Node> chain, Dictionary<Node, (double Lat, double Lon)> nodeCentroid,
        int tier, int minCount, int maxCount)
    {
        var coords = new StringBuilder("[");
        for (int i = 0; i < chain.Count; i++)
        {
            if (i > 0) coords.Append(",");
            var (lat, lon) = nodeCentroid[chain[i]];
            coords.Append('[').Append(Fmt(lon)).Append(',').Append(Fmt(lat)).Append(']');
        }
        coords.Append("]");

        return "{\"type\":\"Feature\",\"properties\":{"
             + "\"minCount\":" + minCount
             + ",\"maxCount\":" + maxCount
             + ",\"weight\":" + TierWeights[tier].ToString("F2", CultureInfo.InvariantCulture)
             + ",\"opacity\":" + TierOpacities[tier].ToString("F2", CultureInfo.InvariantCulture)
             + "},\"geometry\":{\"type\":\"LineString\",\"coordinates\":" + coords + "}}";
    }

    static void AddAdjacency(Dictionary<Node, List<(Node, EdgeKey)>> adjacency, Node from, Node to, EdgeKey key)
    {
        if (!adjacency.TryGetValue(from, out var list))
            adjacency[from] = list = new List<(Node, EdgeKey)>();
        list.Add((to, key));
    }

    static int TierForCount(int count) =>
        Math.Clamp((int)Math.Floor(Math.Log2(Math.Max(1, count))), 0, TierWeights.Length - 1);

    static (double LatStep, double LonStep) GridSteps(double refLat)
    {
        const double metersPerDegreeLat = 111_320.0;
        double metersPerDegreeLon = Math.Max(metersPerDegreeLat * Math.Cos(refLat * Math.PI / 180.0), 1.0);
        return (GridSizeMeters / metersPerDegreeLat, GridSizeMeters / metersPerDegreeLon);
    }

    /// <summary>Snap coordinates to grid cells, collapsing consecutive duplicates --
    /// this doubles as simplification for GPS jitter. Every raw point is also
    /// accumulated into <paramref name="centroidSums"/> for whichever cell it fell
    /// into, regardless of collapsing -- see BuildTypeFeatureCollection for why.</summary>
    static List<Node> SnapAndCollapse(
        List<(double Lat, double Lon)> points, double latStep, double lonStep,
        Dictionary<Node, (double SumLat, double SumLon, int Count)> centroidSums)
    {
        var cells = new List<Node>();
        foreach (var p in points)
        {
            var cell = ToCell(p, latStep, lonStep);
            if (cells.Count == 0 || !cells[^1].Equals(cell)) cells.Add(cell);

            centroidSums[cell] = centroidSums.TryGetValue(cell, out var acc)
                ? (acc.SumLat + p.Lat, acc.SumLon + p.Lon, acc.Count + 1)
                : (p.Lat, p.Lon, 1);
        }
        return cells;
    }

    static Node ToCell((double Lat, double Lon) p, double latStep, double lonStep) =>
        new((long)Math.Round(p.Lat / latStep), (long)Math.Round(p.Lon / lonStep));

    static EdgeKey MakeEdgeKey(Node a, Node b)
    {
        bool aFirst = a.Lat < b.Lat || (a.Lat == b.Lat && a.Lon <= b.Lon);
        return aFirst ? new EdgeKey(a.Lat, a.Lon, b.Lat, b.Lon) : new EdgeKey(b.Lat, b.Lon, a.Lat, a.Lon);
    }

    static string Fmt(double d) => d.ToString("F6", CultureInfo.InvariantCulture);
    static string JsonStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
