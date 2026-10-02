using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Walkability grid + A* path finder for bots, built lazily from the Floor tilemap and the
/// obstacle colliders (walls, destructible blocks, props). Destroyed terrain is picked up
/// automatically. One cell = one tile; paths are smoothed so bots cut corners cleanly and keep a
/// body's width away from walls.
/// </summary>
public class NavGrid : MonoBehaviour
{
    private static NavGrid _instance;

    public static NavGrid Get()
    {
        if (_instance != null) return _instance;
        var floor = GameObject.Find("Floor");
        if (floor == null || !floor.TryGetComponent<Tilemap>(out var map)) return null;

        var go = new GameObject("NavGrid");
        _instance = go.AddComponent<NavGrid>();
        _instance._floor = map;
        return _instance;
    }

    private Tilemap _floor;
    private int _w, _h, _x0, _y0;
    private bool[] _walk;          // true = a body can stand in this cell
    private bool[] _near;          // true = walkable but hugging a wall (slightly costlier)
    private bool _built;
    private LayerMask _mask;
    private float _dirtyAt = -1f;  // time at which the terrain-changed cells get re-scanned
    private readonly List<Vector3Int> _dirty = new();

    // A* scratch (allocated once)
    private float[] _g;
    private int[] _parent;
    private int[] _stamp;
    private int _gen;
    private readonly List<int> _open = new();

    private static readonly Vector2Int[] Dirs =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    private void Awake() => _mask = LayerMask.GetMask("Obstacle", "Destructible");
    private void OnEnable() => DestructibleTilemap.Carved += OnCarved;
    private void OnDisable() => DestructibleTilemap.Carved -= OnCarved;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    /// <summary>False until physics has had a step to build the tilemap colliders.</summary>
    public bool Ready => Time.timeSinceLevelLoad > 0.3f;

    private int _lastPathFrame = -1;
    /// <summary>One path search per frame keeps many bots from causing a frame spike together.</summary>
    public bool CanPathNow => _lastPathFrame != Time.frameCount;

    // ------------------------------------------------------------------ building
    private void Build()
    {
        _floor.CompressBounds();
        var b = _floor.cellBounds;
        _x0 = b.xMin; _y0 = b.yMin; _w = b.size.x; _h = b.size.y;
        _walk = new bool[_w * _h];
        _near = new bool[_w * _h];
        _g = new float[_w * _h];
        _parent = new int[_w * _h];
        _stamp = new int[_w * _h];

        for (int y = 0; y < _h; y++)
        for (int x = 0; x < _w; x++)
            _walk[y * _w + x] = ScanCell(new Vector3Int(_x0 + x, _y0 + y, 0));
        for (int y = 0; y < _h; y++)
        for (int x = 0; x < _w; x++)
            _near[y * _w + x] = _walk[y * _w + x] && TouchesBlocked(x, y);
        _built = true;
    }

    private bool ScanCell(Vector3Int cell)
    {
        if (!_floor.HasTile(cell)) return false;
        Vector2 c = _floor.GetCellCenterWorld(cell);
        return Physics2D.OverlapBox(c, new Vector2(0.9f, 0.9f), 0f, _mask) == null;
    }

    private bool TouchesBlocked(int x, int y)
    {
        foreach (var d in Dirs)
        {
            int nx = x + d.x, ny = y + d.y;
            if (nx < 0 || ny < 0 || nx >= _w || ny >= _h || !_walk[ny * _w + nx]) return true;
        }
        return false;
    }

    private void OnCarved(Vector2 centre, float radius)
    {
        if (!_built) return;
        int r = Mathf.CeilToInt(radius) + 1;
        var cc = _floor.WorldToCell(centre);
        for (int x = -r; x <= r; x++)
        for (int y = -r; y <= r; y++) _dirty.Add(new Vector3Int(cc.x + x, cc.y + y, 0));
        _dirtyAt = Time.time + 0.25f; // let the tilemap collider rebuild first
    }

    private void Update()
    {
        if (_dirtyAt < 0f || Time.time < _dirtyAt) return;
        _dirtyAt = -1f;
        foreach (var cell in _dirty)
        {
            int x = cell.x - _x0, y = cell.y - _y0;
            if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
            _walk[y * _w + x] = ScanCell(cell);
        }
        foreach (var cell in _dirty)
        {
            int x = cell.x - _x0, y = cell.y - _y0;
            if (x < 1 || y < 1 || x >= _w - 1 || y >= _h - 1) continue;
            foreach (var d in Dirs) // neighbours' "hugging wall" flags may have changed too
            {
                int nx = x + d.x, ny = y + d.y;
                _near[ny * _w + nx] = _walk[ny * _w + nx] && TouchesBlocked(nx, ny);
            }
            _near[y * _w + x] = _walk[y * _w + x] && TouchesBlocked(x, y);
        }
        _dirty.Clear();
    }

    // ------------------------------------------------------------------ queries
    private int CellIndex(Vector2 p, out bool inside)
    {
        var c = _floor.WorldToCell(p);
        int x = c.x - _x0, y = c.y - _y0;
        inside = x >= 0 && y >= 0 && x < _w && y < _h;
        return inside ? y * _w + x : -1;
    }

    private Vector2 Centre(int i) => _floor.GetCellCenterWorld(new Vector3Int(_x0 + i % _w, _y0 + i / _w, 0));

    /// <summary>A random walkable spot between minDist and maxDist from 'from' (used for wandering).</summary>
    public bool TryRandomPoint(Vector2 from, float minDist, float maxDist, out Vector2 point)
    {
        point = from;
        if (!Ready) return false;
        if (!_built) Build();

        for (int i = 0; i < 40; i++)
        {
            int idx = Random.Range(0, _walk.Length);
            if (!_walk[idx] || _near[idx]) continue;       // open ground only, not hugging a wall
            Vector2 c = Centre(idx);
            float d = Vector2.Distance(c, from);
            if (d < minDist || d > maxDist) continue;
            point = c;
            return true;
        }
        return false;
    }

    /// <summary>Nearest walkable cell to a point (searches a few rings out), or -1.</summary>
    private int Snap(Vector2 p)
    {
        int i = CellIndex(p, out bool inside);
        if (!inside) return -1;
        if (_walk[i]) return i;

        int cx = i % _w, cy = i / _w;
        for (int r = 1; r <= 3; r++)
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
                if (_walk[y * _w + x]) return y * _w + x;
            }
        return -1;
    }

    public bool IsWalkable(Vector2 p)
    {
        if (!Ready) return true;
        if (!_built) Build();
        int i = CellIndex(p, out bool inside);
        return inside && _walk[i];
    }

    /// <summary>
    /// Finds a route from 'from' to 'to'. Fills 'path' with world-space waypoints (excluding the
    /// start, ending at the goal). Returns false if no route exists or the grid isn't ready yet.
    /// </summary>
    public bool FindPath(Vector2 from, Vector2 to, List<Vector2> path, float bodyRadius = 0.4f)
    {
        path.Clear();
        if (!Ready) return false;
        _lastPathFrame = Time.frameCount;
        if (!_built) Build();

        int start = Snap(from), goal = Snap(to);
        if (start < 0 || goal < 0) return false;
        if (start == goal) { path.Add(to); return true; }

        _gen++;
        _open.Clear();
        Set(start, 0f, -1);
        _open.Add(start);

        int goalX = goal % _w, goalY = goal / _w;
        int expanded = 0;

        while (_open.Count > 0 && expanded++ < 4000)
        {
            // pop lowest f (the open list stays small on these map sizes, so a linear scan is cheap)
            int bi = 0; float bf = float.MaxValue;
            for (int k = 0; k < _open.Count; k++)
            {
                int n = _open[k];
                float f = _g[n] + Heur(n % _w, n / _w, goalX, goalY);
                if (f < bf) { bf = f; bi = k; }
            }
            int cur = _open[bi];
            _open[bi] = _open[_open.Count - 1];
            _open.RemoveAt(_open.Count - 1);

            if (cur == goal) { Build(path, start, goal, from, to, bodyRadius); return true; }

            int cx = cur % _w, cy = cur / _w;
            foreach (var d in Dirs)
            {
                int nx = cx + d.x, ny = cy + d.y;
                if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                int ni = ny * _w + nx;
                if (!_walk[ni]) continue;

                bool diag = d.x != 0 && d.y != 0;
                // no squeezing through diagonal gaps between two blocked cells
                if (diag && (!_walk[cy * _w + nx] || !_walk[ny * _w + cx])) continue;

                float step = (diag ? 1.414f : 1f) + (_near[ni] ? 0.8f : 0f);
                float ng = _g[cur] + step;
                if (_stamp[ni] == _gen && ng >= _g[ni]) continue;

                bool wasOpen = _stamp[ni] == _gen;
                Set(ni, ng, cur);
                if (!wasOpen) _open.Add(ni);
            }
        }
        return false;
    }

    private void Set(int i, float g, int parent) { _stamp[i] = _gen; _g[i] = g; _parent[i] = parent; }

    private static float Heur(int x, int y, int gx, int gy)
    {
        float dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
        return (dx + dy) + (1.414f - 2f) * Mathf.Min(dx, dy); // octile distance
    }

    private void Build(List<Vector2> path, int start, int goal, Vector2 from, Vector2 to, float radius)
    {
        var cells = new List<Vector2>();
        for (int n = goal; n != -1 && n != start; n = _parent[n]) cells.Add(Centre(n));
        cells.Reverse();
        if (cells.Count == 0) { path.Add(to); return; }
        cells[cells.Count - 1] = to; // end exactly on the requested point (it may be off-centre)

        // String-pull: skip waypoints whenever a body-width sweep to a later one is clear.
        Vector2 anchor = from;
        int i = 0;
        while (i < cells.Count)
        {
            int far = i;
            for (int j = cells.Count - 1; j > i; j--)
            {
                Vector2 d = cells[j] - anchor;
                if (Physics2D.CircleCast(anchor, radius, d.normalized, d.magnitude, _mask).collider == null) { far = j; break; }
            }
            path.Add(cells[far]);
            anchor = cells[far];
            i = far + 1;
        }
    }
}
