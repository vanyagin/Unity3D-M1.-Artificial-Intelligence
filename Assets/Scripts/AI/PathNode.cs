using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathNode : IComparable<PathNode>
{
    public bool walkable;           //  Свободна для перемещения
    public Vector3 worldPosition;   //  Позиция в глобальных координатах
    private GameObject objPrefab;   //  Шаблон объекта
    public GameObject body;         //  Объект для отрисовки
    
    private PathNode parentNode = null;               //  откуда пришли

    public Vector2Int gridPos;

    public int x
    {
        get { return gridPos.x; }
    }
    public int y
    {
        get { return gridPos.y; }
    }

    /// <summary>
    /// Родительская вершина - предшествующая текущей в пути от начальной к целевой
    /// </summary>
    public PathNode ParentNode
    {
        get => parentNode;
        set => SetParent(value);
    }

    private float distance = float.PositiveInfinity;  //  расстояние от начальной вершины

    /// <summary>
    /// Расстояние от начальной вершины до текущей (+infinity если ещё не развёртывали)
    /// </summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    /// <summary>
    /// Устанавливаем родителя и обновляем расстояние от него до текущей вершины. Неоптимально - дважды расстояние считается

    private void SetParent(PathNode parent)
    {
        //  Указываем родителя
        parentNode = parent;
        //  Вычисляем расстояние
        if (parent != null)
            distance = parent.Distance + Dist(parent, this);
        else
            distance = float.PositiveInfinity;
    }

    /// <summary>
    /// Конструктор вершины
    /// </summary>

    public PathNode(GameObject _objPrefab, bool _walkable, Vector2Int _gridPos, Vector3 position)
    {
        objPrefab = _objPrefab;
        walkable = _walkable;
        worldPosition = position;
        gridPos = _gridPos;
        body = GameObject.Instantiate(objPrefab, worldPosition, Quaternion.identity);
    }

    /// <summary>
    /// Расстояние между вершинами - разброс по высоте учитывается дополнительно
    /// </summary>

    /// <returns></returns>
    public static float Dist(PathNode a, PathNode b)
    {
        float diff = b.body.transform.position.y - a.body.transform.position.y;
        if (diff < 0) diff = 0;
        return Vector3.Distance(a.body.transform.position, b.body.transform.position) + 40 * Math.Abs(a.body.transform.position.y - b.body.transform.position.y);
        //return Vector3.Distance(a.body.transform.position, b.body.transform.position) + 40 * diff;
    }

    public static float Heuristic(PathNode a, PathNode b)
    {
        return Vector3.Distance(a.body.transform.position, b.body.transform.position);
    }


    /// <summary>
    /// Подсветить вершину - перекрасить в красный
    /// </summary>
    public void Illuminate()
    {
        body.GetComponent<Renderer>().material.color = Color.red;
    }
    public void FadeToYellow()
    {
        body.GetComponent<Renderer>().material.color = Color.yellow;
    }

    public void FadeToGreen()
    {
        body.GetComponent<Renderer>().material.color = Color.green;
    }

    /// <summary>
    /// Снять подсветку с вершины - перекрасить в синий
    /// </summary>
    public void Fade()
    {
        body.GetComponent<Renderer>().material.color = Color.blue;
    }

    public int CompareTo(PathNode other)
    {
        if (this.distance > other.distance)
        {
            return 1;
        }

        if (this.distance == other.distance)
        {
            return 0;
        }
        return -1;
    }

}
