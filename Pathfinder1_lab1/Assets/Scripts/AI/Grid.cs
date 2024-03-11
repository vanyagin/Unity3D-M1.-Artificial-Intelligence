using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour
{
    //  Модель для отрисовки узла сетки
    public GameObject nodeModel;

    //  Ландшафт (Terrain) на котором строится путь
    [SerializeField] private Terrain landscape = null;

    //  Шаг сетки (по x и z) для построения точек
    [SerializeField] private int gridDelta = 20;

    //  Номер кадра, на котором будет выполнено обновление путей
    private int updateAtFrame = 0;

    //  Массив узлов - создаётся один раз, при первом вызове скрипта
    private PathNode[,] grid = null;

    private void CheckWalkableNodes()
    {
        foreach (PathNode node in grid)
        {
            //  Пока что считаем все вершины проходимыми, без учёта препятствий

            node.walkable = true;

            // 2.5 == nodeModel.GetComponent< SphereCollider>().radius * nodeModel.transform.localScale.x

            node.walkable = !Physics.CheckSphere(node.body.transform.position, 2.5f);
            if (node.walkable)
                node.Fade();
            else
            {
                node.Illuminate();
                Debug.Log("Not walkable!");
            }
        }
    }


    // Метод вызывается однократно перед отрисовкой первого кадра
    void Start()
    {
        //  Создаём сетку узлов для навигации - адаптивную, под размер ландшафта
        Vector3 terrainSize = landscape.terrainData.bounds.size;
        int sizeX = (int)(terrainSize.x / gridDelta);
        int sizeZ = (int)(terrainSize.z / gridDelta);
        //  Создаём и заполняем сетку вершин, приподнимая на 25 единиц над ландшафтом
        grid = new PathNode[sizeX, sizeZ];
        for (int x = 0; x < sizeX; ++x)
            for (int z = 0; z < sizeZ; ++z)
            {
                Vector3 position = new Vector3(x * gridDelta, 0, z * gridDelta);
                position.y = landscape.SampleHeight(position) + 25;
                grid[x, z] = new PathNode(nodeModel, false, new Vector2Int(x, z), position);
                grid[x, z].ParentNode = null;
                grid[x, z].Fade();
            }
    }
    /// <summary>
    /// Получение списка соседних узлов для вершины сетки
    /// </summary>
    /// <param name="current">индексы текущей вершины </param>
    /// <returns></returns>
    private List<Vector2Int> GetNeighbours(Vector2Int current)
    {
        List<Vector2Int> nodes = new List<Vector2Int>();
        for (int x = current.x - 1; x <= current.x + 1; ++x)
            for (int y = current.y - 1; y <= current.y + 1; ++y)
                if (x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) && (x != current.x || y != current.y))
                    nodes.Add(new Vector2Int(x, y));
        return nodes;
    }

    /// <summary>
    /// Вычисление "кратчайшего" между двумя вершинами сетки
    /// </summary>
    /// <param name="startNode">Координаты начального узла пути (индексы элемента в массиве grid)</param>
    /// <param name="finishNode">Координаты конечного узла пути (индексы элемента в массиве grid)</param>
    void calculatePath(Vector2Int startNode, Vector2Int finishNode)
    {
        //  Очищаем все узлы - сбрасываем отметку родителя, снимаем подсветку
        foreach (var node in grid)
        {
            node.Fade();
            node.ParentNode = null;
        }

        CheckWalkableNodes();

        //  Реализуется аналог волнового алгоритма, причём найденный путь не будет являться оптимальным 

        PathNode start = grid[startNode.x, startNode.y];

        //  Начальную вершину отдельно изменяем
        start.ParentNode = null;
        start.Distance = 0;

        //  Очередь вершин в обработке - в A* необходимо заменить на очередь с приоритетом
        Queue<Vector2Int> nodes = new Queue<Vector2Int>();
        //  Начальную вершину помещаем в очередь
        nodes.Enqueue(startNode);
        //  Пока не обработаны все вершины (очередь содержит узлы для обработки)
        while (nodes.Count != 0)
        {
            Vector2Int current = nodes.Dequeue();
            //  Если достали целевую - можно заканчивать (это верно и для A*)
            if (current == finishNode) break;
            //  Получаем список соседей
            var neighbours = GetNeighbours(current);
            foreach (var node in neighbours)
                if (grid[node.x, node.y].walkable && grid[node.x, node.y].Distance > grid[current.x, current.y].Distance + PathNode.Dist(grid[node.x, node.y], grid[current.x, current.y]))
                {
                    grid[node.x, node.y].ParentNode = grid[current.x, current.y];
                    nodes.Enqueue(node);
                }
        }
        //  Восстанавливаем путь от целевой к стартовой
        var pathElem = grid[finishNode.x, finishNode.y];
        while (pathElem != null)
        {
            pathElem.FadeToYellow();
            pathElem = pathElem.ParentNode;
        }
    }


    void Dijkstra(Vector2Int startNode, Vector2Int finishNode)
    {
        //  Очищаем все узлы - сбрасываем отметку родителя, снимаем подсветку
        foreach (var node in grid)
        {
            node.Fade();
            node.ParentNode = null;
        }

        //  На данный момент вызов этого метода не нужен, там только устанавливается проходимость вершины. Можно добавить обработку препятствий
        CheckWalkableNodes();


        PathNode start = grid[startNode.x, startNode.y];
        //  Начальную вершину отдельно изменяем
        start.ParentNode = null;
        start.Distance = 0;

        //  Очередь вершин в обработке - в A* необходимо заменить на очередь с приоритетом
        var nodes = new Queue<PathNode>();
        //  Начальную вершину помещаем в очередь
        nodes.Enqueue(start);
        //  Пока не обработаны все вершины (очередь содержит узлы для обработки)
        while (nodes.Count != 0)
        {
            var current = nodes.Dequeue();
            //  Если достали целевую - можно заканчивать (это верно и для A*)
            if (current.gridPos == finishNode) break;
            //  Получаем список соседей
            var neighbours = GetNeighbours(current.gridPos);
            foreach (var n in neighbours)
            {
                var neighbour = grid[n.x, n.y];
                if (neighbour.walkable && (current.Distance + PathNode.Dist(neighbour, current) <= neighbour.Distance))
                {
                    neighbour.ParentNode = current;
                    nodes.Enqueue(neighbour);
                }
            }
        }
        //  Восстанавливаем путь от целевой к стартовой
        var pathElem = grid[finishNode.x, finishNode.y];
        while (pathElem != null)
        {
            pathElem.FadeToYellow();
            pathElem = pathElem.ParentNode;
        }
    }


    void Astar(Vector2Int startNode, Vector2Int finishNode)
    {
        //  Очищаем все узлы - сбрасываем отметку родителя, снимаем подсветку
        foreach (var node in grid)
        {
            node.Fade();
            node.ParentNode = null;
        }

        //  На данный момент вызов этого метода не нужен, там только устанавливается проходимость вершины. Можно добавить обработку препятствий
        CheckWalkableNodes();

        //  Реализуется аналог волнового алгоритма, причём найденный путь не будет являться оптимальным 

        PathNode start = grid[startNode.x, startNode.y];
        PathNode finish = grid[finishNode.x, finishNode.y];
        //  Начальную вершину отдельно изменяем
        start.ParentNode = null;
        start.Distance = 0;

        //  Очередь вершин в обработке - в A* необходимо заменить на очередь с приоритетом
        var nodes = new PriorityQueue<PathNode>();
        var visitedNodes = new HashSet<PathNode>();
        //  Начальную вершину помещаем в очередь
        nodes.Enqueue(start);
        //  Пока не обработаны все вершины (очередь содержит узлы для обработки)
        while (nodes.Count != 0)
        {
            var current = nodes.Dequeue();
            //  Если достали целевую - можно заканчивать (это верно и для A*)
            // Проверка посещенных вершин 
            if (visitedNodes.Contains(current) == true) continue;
            if (current.gridPos == finishNode) break;
            //  Получаем список соседей
            var neighbours = GetNeighbours(current.gridPos);
            foreach (var n in neighbours)
            {
                var neighbour = grid[n.x, n.y];
                if (neighbour.walkable && (current.Distance  + PathNode.Heuristic(current, finish) <= neighbour.Distance))
                {
                    neighbour.ParentNode = current;
                    //neighbour.Distance += PathNode.Heuristic(neighbour, finish);
                    nodes.Enqueue(neighbour);
                    visitedNodes.Add(current);
                }
            }
        }
        //  Восстанавливаем путь от целевой к стартовой
        var pathElem = grid[finishNode.x, finishNode.y];
        while (pathElem != null)
        {
            pathElem.FadeToGreen();
            pathElem = pathElem.ParentNode;
        }
    }

    // Метод вызывается каждый кадр
    void Update()
    {
        //  Чтобы не вызывать этот метод каждый кадр, устанавливаем интервал вызова в 1000 кадров
        if (Time.frameCount < updateAtFrame) return;

        updateAtFrame = Time.frameCount + 300;

        //Dijkstra(new Vector2Int(0, 0), new Vector2Int(grid.GetLength(0) - 1, grid.GetLength(1) - 1));
        Astar(new Vector2Int(0, 0), new Vector2Int(grid.GetLength(0) - 1, grid.GetLength(1) - 1));

    }
}