// BoardManager.cs  (POOLING + Deadlock + SmartShuffle + Dynamic Icon)

using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public int rows = 8;
    public int columns = 8;
    public GameObject blockPrefab;
    public Color[] colors;

    // Dinamik ikon eşikleri
    public int A = 3;
    public int B = 5;
    public int C = 7;

    // Pool ayarı (istersen Inspector’dan arttır)
    public int prewarmPoolCount = 64;

    private Tile[,] grid;

    // ---- POOL ----
    private Queue<GameObject> pool = new Queue<GameObject>();
    private Transform poolRoot;

    void Start()
    {
        Debug.Log("BoardManager START");

        // Clamp board sizes according to case constraints
        rows = Mathf.Clamp(rows, 2, 10);
        columns = Mathf.Clamp(columns, 2, 10);

        if (blockPrefab == null)
        {
            Debug.LogError("Block Prefab is NOT assigned on BoardManager!");
            return;
        }

        if (colors == null || colors.Length == 0)
        {
            Debug.LogError("Colors array is EMPTY. Set Colors Size > 0 in Inspector!");
            return;
        }

        InitPool();
        PrewarmPool(prewarmPoolCount);

        CreateBoard();
        AfterBoardSettled();
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (Camera.main == null)
        {
            Debug.LogError("Main Camera not found! Make sure Main Camera tag = MainCamera.");
            return;
        }

        Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
        if (hit.collider == null) return;

        Tile clicked = hit.collider.GetComponent<Tile>();
        if (clicked == null) return;

        if (!InBounds(clicked.x, clicked.y)) return;
        if (grid[clicked.x, clicked.y] == null) return;

        List<Tile> group = FindGroup(clicked.x, clicked.y);
        Debug.Log($"Group size = {group.Count} (clicked: {clicked.x},{clicked.y})");

        if (group.Count < 2) return;

        BlastGroup(group);
        ApplyGravityAndRefill();
    }

    // =========================
    // POOLING
    // =========================
    void InitPool()
    {
        var existing = transform.Find("_Pool");
        if (existing != null) poolRoot = existing;
        else
        {
            GameObject pr = new GameObject("_Pool");
            pr.transform.SetParent(transform);
            pr.transform.localPosition = Vector3.zero;
            poolRoot = pr.transform;
        }
    }

    void PrewarmPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(blockPrefab);
            go.SetActive(false);
            go.transform.SetParent(poolRoot);
            pool.Enqueue(go);
        }
    }

    GameObject PoolGet(Vector2 pos)
    {
        GameObject go;
        if (pool.Count > 0)
        {
            go = pool.Dequeue();
        }
        else
        {
            go = Instantiate(blockPrefab);
        }

        go.transform.SetParent(transform);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.SetActive(true);

        return go;
    }

    void PoolRelease(GameObject go)
    {
        if (go == null) return;

        go.SetActive(false);
        go.transform.SetParent(poolRoot);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        pool.Enqueue(go);
    }

    // =========================
    // BOARD
    // =========================
    void CreateBoard()
    {
        // Eski grid varsa: içindekileri pool'a geri bırak
        if (grid != null)
        {
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (grid[x, y] != null)
                    {
                        PoolRelease(grid[x, y].gameObject);
                        grid[x, y] = null;
                    }
                }
            }
        }

        grid = new Tile[columns, rows];

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                SpawnTileAt(x, y);
            }
        }

        Debug.Log("Board created!");
    }

    void SpawnTileAt(int x, int y)
    {
        Vector2 pos = new Vector2(x * 1.1f, y * 1.1f);
        GameObject go = PoolGet(pos);

        Tile tile = go.GetComponent<Tile>();
        if (tile == null)
        {
            Debug.LogError("Tile component is missing on Block prefab! Add Tile script to Block prefab.");
            PoolRelease(go);
            return;
        }

        Color c = colors[Random.Range(0, colors.Length)];

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = c;

        tile.x = x;
        tile.y = y;
        tile.color = c;

        grid[x, y] = tile;
    }

    void BlastGroup(List<Tile> group)
    {
        foreach (Tile t in group)
        {
            if (t == null) continue;
            if (!InBounds(t.x, t.y)) continue;

            grid[t.x, t.y] = null;

            // Destroy yerine pool'a geri
            PoolRelease(t.gameObject);
        }

        Debug.Log($"Blasted {group.Count} tiles!");
    }

    void ApplyGravityAndRefill()
    {
        for (int x = 0; x < columns; x++)
        {
            int writeY = 0;

            for (int y = 0; y < rows; y++)
            {
                if (grid[x, y] != null)
                {
                    if (y != writeY)
                    {
                        Tile t = grid[x, y];
                        grid[x, writeY] = t;
                        grid[x, y] = null;

                        t.x = x;
                        t.y = writeY;
                        t.transform.position = new Vector2(x, writeY);
                    }
                    writeY++;
                }
            }

            for (int y = writeY; y < rows; y++)
            {
                SpawnTileAt(x, y);
            }
        }

        Debug.Log("Gravity+Refill done!");
        AfterBoardSettled();
    }

    // =========================
    // GROUP (BFS)
    // =========================
    List<Tile> FindGroup(int startX, int startY)
    {
        List<Tile> result = new List<Tile>();
        bool[,] visited = new bool[columns, rows];

        Tile startTile = grid[startX, startY];
        if (startTile == null) return result;

        Color targetColor = startTile.color;

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        while (q.Count > 0)
        {
            Vector2Int p = q.Dequeue();
            Tile t = grid[p.x, p.y];

            if (t == null) continue;
            if (t.color != targetColor) continue;

            result.Add(t);

            TryEnqueue(p.x + 1, p.y);
            TryEnqueue(p.x - 1, p.y);
            TryEnqueue(p.x, p.y + 1);
            TryEnqueue(p.x, p.y - 1);
        }

        return result;

        void TryEnqueue(int nx, int ny)
        {
            if (nx < 0 || nx >= columns || ny < 0 || ny >= rows) return;
            if (visited[nx, ny]) return;
            visited[nx, ny] = true;
            q.Enqueue(new Vector2Int(nx, ny));
        }
    }

    bool InBounds(int x, int y) => x >= 0 && x < columns && y >= 0 && y < rows;

    // =========================
    // DEADLOCK
    // =========================
    bool HasAnyMove()
    {
        bool[,] visited = new bool[columns, rows];

        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
        {
            if (grid[x, y] == null) continue;
            if (visited[x, y]) continue;

            List<Tile> g = FindGroup(x, y);
            foreach (var t in g)
                if (t != null) visited[t.x, t.y] = true;

            if (g.Count >= 2) return true;
        }

        return false;
    }

    // =========================
    // SMART SHUFFLE (garanti 2'li)
    // =========================
    void SmartShuffle()
    {
        List<Color> bag = new List<Color>();

        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
            if (grid[x, y] != null) bag.Add(grid[x, y].color);

        if (bag.Count == 0) return;

        Color matchColor = bag[Random.Range(0, bag.Count)];
        int sx = Random.Range(0, columns - 1);
        int sy = Random.Range(0, rows);

        for (int i = 0; i < bag.Count; i++)
        {
            int j = Random.Range(i, bag.Count);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        int idx = 0;
        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
        {
            var t = grid[x, y];
            if (t == null) continue;

            SetTileColor(t, bag[idx++]);
        }

        if (grid[sx, sy] != null) SetTileColor(grid[sx, sy], matchColor);
        if (grid[sx + 1, sy] != null) SetTileColor(grid[sx + 1, sy], matchColor);

        Debug.Log("SmartShuffle done!");
    }

    void SetTileColor(Tile t, Color c)
    {
        t.color = c;
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = c;
        t.transform.localScale = Vector3.one;
    }

    // =========================
    // DYNAMIC ICON (sprite yoksa scale)
    // =========================
    
    void UpdateGroupIcons()
{
    bool[,] visited = new bool[columns, rows];

    for (int x = 0; x < columns; x++)
    for (int y = 0; y < rows; y++)
    {
        if (grid[x, y] == null) continue;
        if (visited[x, y]) continue;

        List<Tile> g = FindGroup(x, y);
        foreach (var t in g)
            if (t != null) visited[t.x, t.y] = true;

        // tier seçimi (default=0)
        int tier = 0;
        if (g.Count >= C) tier = 3;      // third icon
        else if (g.Count >= B) tier = 2; // second icon
        else if (g.Count >= A) tier = 1; // first icon
        else tier = 0;                  // default

        foreach (var t in g)
        {
            if (t == null) continue;

            var vis = t.GetComponent<TileVisual>();
            if (vis != null)
                vis.SetTier(tier);
        }
    }
}


    // =========================
    // SETTLED FLOW
    // =========================
    void AfterBoardSettled()
    {
        UpdateGroupIcons();

        if (!HasAnyMove())
        {
            Debug.Log("DEADLOCK detected -> SmartShuffle!");
            SmartShuffle();
            UpdateGroupIcons();
        }
    }
}
