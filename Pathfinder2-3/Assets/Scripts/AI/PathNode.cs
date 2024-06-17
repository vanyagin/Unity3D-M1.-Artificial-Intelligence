using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BaseAI
{
    /// <summary>
    /// Точка пути - изменяем по сравенению с предыдущим проектом
    /// </summary>
    public class PathNode//: MonoBehaviour
    {
        public Vector3 Position { get; set; }         //  Позиция в глобальных координатах
        public Vector3 Direction { get; set; }        //  Направление
        public float TimeMoment { get; set; }         //  Момент времени       
        /// <summary>
        /// Нужно ли из этой вершины прыгать
        /// </summary>
        public bool JumpNode { get; set; } = false; 
        /// <summary>
        /// Родительская вершина - предшествующая текущей в пути от начальной к целевой
        /// </summary>
        public PathNode Parent = null;       //  Родительский узел

        public float H { get; set; }  //  Оставшийся путь до цели

        /// <summary>
        /// Предположительный индекс региона, в котором находится точка. Проблема в том, что регионы могут накладываться
        /// друг на друга, и этот индекс может не соответствовать тому, который нам нужен
        /// </summary>
        public int RegionIndex = -1;  

        /// <summary>
        /// Конструирование вершины на основе родительской (если она указана)
        /// </summary>
        /// <param name="ParentNode">Если существует родительская вершина, то её указываем</param>
        public PathNode(PathNode ParentNode = null)
        {
            Parent = ParentNode;
        }

        /// <summary>
        /// Конструирование вершины на основе родительской (если она указана)
        /// </summary>
        /// <param name="ParentNode">Если существует родительская вершина, то её указываем</param>
        public PathNode(Vector3 currentPosition, Vector3 currentDirection)
        {
            Position = currentPosition;      //  Позицию задаём
            Direction = currentDirection;    //  Направление отсутствует
            TimeMoment = Time.fixedTime;     //  Время текущее
            Parent = null;                   //  Родителя нет
            RegionIndex = -1;
            H = 0;
        }

        /// <summary>
        /// Расстояние между точками без учёта времени. Со временем - отдельная история
        /// Если мы рассматриваем расстояние до целевой вершины, то непонятно как учитывать время
        /// </summary>
        /// <param name="other">Точка, до которой высчитываем расстояние</param>
        /// <returns></returns>
        public float Distance(PathNode other)
        {
            return Vector3.Distance(Position, other.Position);
        }

        /// <summary>
        /// Расстояние между точками без учёта времени. Со временем - отдельная история
        /// Если мы рассматриваем расстояние до целевой вершины, то непонятно как учитывать время
        /// </summary>
        /// <param name="other">Точка, до которой высчитываем расстояние</param>
        /// <returns></returns>
        public float Distance(Vector3 other)
        {
            return Vector3.Distance(Position, other);
        }

        /// <summary>
        /// Порождаем дочернюю точку с указанными шагом, углом поворота и дельтой по времени
        /// G и Н не пересчитываются !!!!
        /// </summary>
        /// <param name="stepLength">Длина шага</param>
        /// <param name="rotationAngle">Угол поворота вокруг оси OY в градусах</param>
        /// <param name="timeDelta">Впремя, потраченное на шаг</param>
        /// <returns></returns>
        public PathNode SpawnChild(float stepLength, float rotationAngle, float timeDelta)
        {
            PathNode result = new PathNode(this);

            //  Вращаем вокруг вертикальной оси, что в принципе не очень хорошо - надо бы более универсально, нормаль к поверхности взять, и всё такое
            result.Direction = Quaternion.AngleAxis(rotationAngle, Vector3.up) * Direction;
            result.Direction.Normalize();

            //  Перемещаемся в новую позицию
            result.Position = Position + result.Direction * stepLength;
            result.Position = new Vector3(result.Position.x, Position.y, result.Position.z);

            //  Момент времени считаем
            result.TimeMoment = TimeMoment + timeDelta;
            
            result.RegionIndex = RegionIndex;

            //  Добавка для эвристики - нужна ли?
            //if (Mathf.Abs(rotationAngle) > 0.001f) result.TimeMoment += 0.3f;

            return result;
        }
        /// <summary>
        /// Точка прыжка вперёд - для проверки в PathFinder
        /// На прыжок не влияют модификаторы динамических платформ
        /// </summary>
        /// <param name="mp"></param>
        /// <returns></returns>
        public Vector3 ForwardJumpPoint(MovementProperties mp)
        {
            return Position + mp.jumpLength * Direction;
        }

        public PathNode SpawnJumpForward(MovementProperties mp)
        {
            PathNode result = new PathNode(this);

            result.Direction = Direction;

            //  Перемещаемся в новую позицию
            result.Position = Position + Direction * mp.jumpLength;

            //  Момент времени считаем
            result.TimeMoment = TimeMoment + mp.jumpTime;

            //  Индекс региона должен быть пересчитан
            result.RegionIndex = -1;

            result.JumpNode = true;

            return result;
        }

        /// <summary>
        /// Дискретизация положения точки к неторому узлу пространственной сетки.
        /// Используется для того, чтобы контролировать какие точки мы уже посещали, в коллекциях типа HashSet
        /// Поворот не учитывается, что вообще-то не очень хорошо
        /// </summary>
        /// <param name="distDelta">Шаг дискретизации по пространству</param>
        /// <param name="timeDelta">Шаг дискретизации по времени</param>
        /// <returns>Четыре координаты (пространство-время)</returns>
        public (int, int, int, int) ToGrid4DPoint(float distDelta, float timeDelta)
        {
            return (Mathf.RoundToInt(Position.x / distDelta),
                Mathf.RoundToInt(Position.y / distDelta),
                Mathf.RoundToInt(Position.z / distDelta),
                Mathf.RoundToInt(TimeMoment / timeDelta));
        }

        public (int, int, int, int, Vector3) ToGrid5DPoint(float distDelta, float timeDelta)
        {
            return (Mathf.RoundToInt(Position.x / distDelta),
                Mathf.RoundToInt(Position.y / distDelta),
                Mathf.RoundToInt(Position.z / distDelta),
                Mathf.RoundToInt(TimeMoment / timeDelta),
                Direction);
        }
    }
}