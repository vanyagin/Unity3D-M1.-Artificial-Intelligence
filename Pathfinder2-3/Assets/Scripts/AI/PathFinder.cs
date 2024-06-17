using Priority_Queue;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BaseAI
{
    /// <summary>
    /// Делегат для обновления пути - вызывается по завершению построения пути
    /// </summary>
    /// <param name="pathNodes"></param>
    /// /// <returns>Успешно ли построен путь до цели</returns>
    public delegate void UpdatePathListDelegate(List<PathNode> pathNodes);

    /// <summary>
    /// Глобальный маршрутизатор - сделать этого гада через делегаты и работу в отдельном потоке!!!
    /// </summary>
    public class PathFinder : MonoBehaviour
    {
        /// <summary>
        /// Объект сцены, на котором размещены коллайдеры
        /// </summary>
        [SerializeField] private GameObject CollidersCollection;

        /// <summary>
        /// Картограф - класс, хранящий информацию о геометрии уровня, регионах и прочем
        /// </summary>
        [SerializeField] private Cartographer cartographer;

        /// <summary>
        /// Маска слоя с препятствиями (для проверки столкновений)
        /// </summary>
        private int obstaclesLayerMask;

        /// <summary>
        /// 
        /// </summary>
        private float rayRadius;

        public PathFinder()
        {

        }

        /// <summary>
        /// Проверка того, что точка проходима. Необходимо обратиться к коллайдеру, ну ещё и проверить высоту над поверхностью
        /// </summary>
        /// <param name="node">Точка</param>
        /// <returns></returns>
        private bool CheckWalkable(ref PathNode node, IBaseRegion currentRegion, IBaseRegion targetRegion)
        {
            //  Сначала проверяем, принадлежит ли точка целевому региону (ну там и переприсвоим индекс если что)

            //  Первым проверяем целевой регион - это обязательно!
            if (targetRegion.Contains(node))
            {
                node.RegionIndex = targetRegion.index;
            }
            else
                //  Теперь проверяем на принадлежность текущему региону
                if (currentRegion.Contains(node))
                {
                    node.RegionIndex = currentRegion.index;
                }
                else 
                    return false;  //  Не принадлежит ни целевому, ни рабочему
            //if (node.JumpNode && currentRegion.index == node.RegionIndex) return false;

            //  Следующая проверка - на то, что над поверхностью расстояние не слишком большое
            //  Технически, тут можно как-то корректировать высоту - с небольшим шагом, позволить объекту спускаться или подниматься, но в целом это проверку пока что можно отключить

            if (!node.JumpNode)
            {
                float distToFloor = node.Position.y - cartographer.SceneTerrain.SampleHeight(node.Position);
                if (distToFloor > 2.0f || distToFloor < 0.0f)
                {
                    Debug.Log("Incorrect node height");
                    return false;
                }

                //  Ну и осталось проверить препятствия - для движущихся не сработает такая штука, потому что проверка выполняется для
                //  момента времени в будущем.
                //  Но из этой штуки теоретически можно сделать и для перемещающихся препятствий работу - надо будет перемещающиеся
                //  заворачивать в отдельный 
                if (node.Parent != null && Physics.CheckSphere(node.Position, 1.0f, obstaclesLayerMask))
                    return false;
            }
            
            return true;
        }

        private static float Heur(PathNode node, PathNode target, MovementProperties properties)
        {
            //  Эвристику переделать - сейчас учитываются уже затраченное время, оставшееся до цели время и угол поворота
            float angle = Mathf.Abs(Vector3.Angle(node.Direction, target.Position - node.Position)) / properties.rotationAngle;
            return node.TimeMoment + 2 * node.Distance(target) / properties.maxSpeed + angle * properties.deltaTime;
        }

        /// <summary>
        /// Получение списка соседей для некоторой точки.
        /// Координаты текущей точки могут быть как локальными, так и глобальными.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public List<PathNode> GetNeighbours(PathNode node, MovementProperties properties, PathNode target, IBaseRegion currentRegion, IBaseRegion targetRegion)
        {
            //  Вот тут хардкодить не надо, это должно быть в properties
            //  У нас есть текущая точка, и свойства движения (там скорость, всякое такое)

            float step = properties.deltaTime * properties.maxSpeed;

            List<PathNode> result = new List<PathNode>();

            //  Сначала прыжок проверяем, и если он возможен, то на этом и закончим
            //  Прыжок должен допускаться только между регионами с разными признаками динамичности
            if (targetRegion.Dynamic != currentRegion.Dynamic)
            {
                PathNode jumpPlace = node.SpawnJumpForward(properties);
                Debug.DrawLine(jumpPlace.Position, jumpPlace.Position + Vector3.up * 10, Color.cyan, 1000000);
                if (CheckWalkable(ref jumpPlace, currentRegion, targetRegion))
                {
                    //  Ну, она проходима, но этого мало, мы должны оказаться в целевом регионе
                    if (jumpPlace.RegionIndex == targetRegion.index)
                    {

                        //  То есть прыгнуть можно, и это прыжок в другой регион
                        //  Маркируем точку как прыжковую (в которую прыгнуть надо)
                        jumpPlace.JumpNode = true;
                        result.Add(jumpPlace);

                        return result;
                    }
                    return null;
                }
                return null;
            }
            else
            {
                //  А в обычные маршруты прыжки не попадают
                //  Внешний цикл отвечает за длину шага - либо 0 (остаёмся в точке), либо 1 - шагаем вперёд
                for (int mult = 0; mult <= 1; ++mult)
                    //  Внутренний цикл перебирает углы поворота
                    for (int angleStep = -properties.angleSteps; angleStep <= properties.angleSteps; ++angleStep)
                    {
                        PathNode next = node.SpawnChild(step * mult, angleStep * properties.rotationAngle, properties.deltaTime);
                        
                        //  Точка передаётся по ссылке, т.к. возможно обновление региона, которому она принадлежит
                        if (CheckWalkable(ref next, currentRegion, targetRegion))
                        {
                            result.Add(next);
                            Debug.DrawLine(node.Position, next.Position, Color.blue, 10f);
                        }
                    }
            }

            return result;
        }

        /// <summary>
        /// Поиск пути (локальный, sample-based, с отсечением пройденных региончиков)
        /// </summary>
        /// <param name="start">Начальная точка пути</param>
        /// <param name="target">Целевая точка пути</param>
        /// <param name="movementProperties">Параметры движения бота</param>
        /// <param name="updater">Делегат, обновляющий путь у бота - вызывается с построенным путём</param>
        /// <param name="finishPredicate">Условие остановки поиска пути</param>
        /// <returns></returns>
        private float FindPath(PathNode start, PathNode target, int targetRegionIndex, MovementProperties movementProperties, UpdatePathListDelegate updater, System.Func<PathNode, PathNode, bool> finishPredicate)
        {
            var targetRegion = cartographer.regions[targetRegionIndex];
            var currentRegion = cartographer.regions[start.RegionIndex];
            //  Целевой регион определяем

            //  Вот тут вместо equals надо использовать == (как минимум), а лучше измерять расстояние между точками
            //  сравнивая с некоторым epsilon. Хотя может это для каких-то специальных случаев?
            //  if (position.Position.Equals(target.Position)) return new List<PathNode>();
            if (Vector3.Distance(start.Position, target.Position) < movementProperties.epsilon)
            {
                Debug.Log("epsilon: " + movementProperties.epsilon+ " distance = " + Vector3.Distance(start.Position, target.Position));
                updater(null);  //  Это функция, которая обновляет путь в агенте
                return -1f;
            }

            Priority_Queue.SimplePriorityQueue<PathNode> opened = new Priority_Queue.SimplePriorityQueue<PathNode>();

            //  Тут тоже вопрос - а почему с 0 добавляем? Хотя она же сразу извлекается из очереди, не важно
            opened.Enqueue(start, 0);
            int steps = 0;

            //  Посещенные узлы (с некоторым шагом, аналог сетки)
            HashSet<(int, int, int, int,Vector3)> closed = new HashSet<(int, int, int, int,Vector3)>();
            closed.Add(start.ToGrid5DPoint(movementProperties.deltaDist, movementProperties.deltaTime));

            PathNode current = opened.First;
            float largestTime = 0;
            
            while (opened.Count != 0 && steps < 4000)
            {
                steps++;
                current = opened.Dequeue();
                if (current.JumpNode) break;

                // То же, что и ниже???
                if(current.Parent != null)
                {
                    Vector3 toTarget = target.Position - current.Position;
                    if(toTarget.magnitude <= movementProperties.deltaTime * movementProperties.maxSpeed)
                    {
                        float angl = Mathf.Abs(Vector3.Angle(toTarget, current.Position - current.Parent.Position));
                        if(angl < movementProperties.rotationAngle*movementProperties.rotationAngle)
                        {
                            ////
                            float angleTcurP = Mathf.Abs(Vector3.Angle(current.Position - current.Parent.Position,target.Position - current.Parent.Position));
                            current.Position = current.Parent.Position + Quaternion.AngleAxis(angleTcurP, Vector3.up) * (current.Position - current.Parent.Position);
                            toTarget = target.Position - current.Position;
                            Debug.DrawLine(current.Position, current.Position + Vector3.up * 10, Color.cyan, 1000000);
                            Debug.DrawLine(current.Parent.Position, current.Position, Color.cyan, 1000000);
                            ////
                            //  Цель в пределах досягаемости
                            current.TimeMoment += toTarget.magnitude / (movementProperties.deltaTime * movementProperties.maxSpeed);
                            current.Position = target.Position;
                        }
                    }
                }

                //  Тут сложная проверка - если извлечённая вершина находится в одном шаге от цели
                if(finishPredicate(current, target))
                if (Vector3.Distance(current.Position, target.Position) <= movementProperties.epsilon)
                {
                        //  Последнюю точку заменяем целевой - чтобы в точности в неё прийти
                    
                    current.Position = target.Position; // тут мб // надо

                    Debug.Log("Braked by closest point. Steps : " + steps.ToString());
                    break;
                }

                //  Получаем список соседей
                
                var neighbours = GetNeighbours(current, movementProperties, target, currentRegion, targetRegion);
                if (neighbours == null) {
                    Debug.Log("waiting for jump");
                    if (currentRegion.Dynamic) updater(new List<PathNode> { target });
                    else updater(null);
                    return -1f; 
                }
                foreach (var nextNode in neighbours)
                {
                    //  Это отладочная информация - чтобы понять, насколько далеко мы успеваем по времени
                    if (nextNode.TimeMoment > largestTime) largestTime = nextNode.TimeMoment;

                    var discreteNode = nextNode.ToGrid5DPoint(movementProperties.deltaDist, movementProperties.deltaTime);
                    if (!closed.Contains(discreteNode))
                    {
                        nextNode.H = Heur(nextNode, target, movementProperties);
                        opened.Enqueue(nextNode, nextNode.H);
                        closed.Add(discreteNode);
                    }
                }
            }

            if (finishPredicate(current, target) == false)
            {
                Debug.Log("Largest time : " + largestTime);
                Debug.Log("Failed to build a way. Steps : " + steps.ToString());
                updater(null);
                return -1f;
            }

            List<PathNode> result = new List<PathNode>();

            //  Восстанавливаем путь от целевой к стартовой
            //  Может, заменить последнюю на целевую, с той же отметкой по времени? Но тогда с поворотом сложновато получается
            while (current != null)
            {
                if(current.Parent != null) 
                    Debug.DrawLine(current.Position, current.Parent.Position, Color.red, 20f);
                result.Add(current);
                current = current.Parent;
            }

            result.Reverse();

            int timeReduce = 0;
            if (result.Count > 0)
                for(int i = 1; i < result.Count; ++i)
                {
                    if (result[i-1].Position == result[i].Position)
                    {
                        ++timeReduce;
                        --i;
                        result.RemoveAt(i);
                    }
                    result[i].TimeMoment -= timeReduce;
                }

            //  Обновляем результат у бота
            updater(result);
            return result[result.Count - 1].TimeMoment - result[0].TimeMoment;

        }

        /// <summary>
        /// Основной метод поиска пути, запускает работу в отдельном потоке. Аккуратно с асинхронностью - мало ли, вроде бы 
        /// потокобезопасен, т.к. не изменяет данные о регионах сценах и прочем - работает как ReadOnly
        /// </summary>
        /// <returns></returns>
        public bool BuildRoute(PathNode start, PathNode finish, MovementProperties movementProperties, UpdatePathListDelegate updater)
        {
            /*  Эта функция выполняет построение глобального пути. Её задача - определить, находятся ли 
             *  начальная и целевая точка в одном регионе. Если да, то просто запустить локальный
             *  маршрутизатор и построить маршрут в этом регионе.
             *  Иначе, если регионы разные - найти кратчайший маршрут между регионами (это задача
             *  глобального планировщика, и он должен вернуть регион, соседний с текущим – это регион, 
             *  в который должны шагнуть. После этого необходимо использовать другой вариант функции поиска пути - 
             *  с другой эвристикой, в качестве которой можно использовать расстояние до центральной точки целевого региона, 
             *  и другой функций проверки целевого состояния, вместо близости к некоторой точке надо проверять, достигли ли мы 
             *  целевого региона. А кто умеет лямбды в C#?
             *  В целом тут можно банально использовать алгоритм Дейкстры. Можно немного усложнить, проверяя
             *  расстояние до границ текущего региона, и как-то до цели, но это уже улучшения. Вообще до 
             *  bounds можно это самое расстояние считать как-то. В базовой версии никаких особых извращений не нужно. 
             *  Можно, конечно, и не Дейкстру, ну или модифицировать его немного.
            */

            //  Получить регион текущей позиции
            //  Если целевая в другом регионе - построить глобальный путь, получить следующий регион
            //  Построить маршрут до следующего региона
            start.Parent = null;
            start.TimeMoment = Time.fixedTime;

            IBaseRegion startRegion = null;
            if (start.RegionIndex == -1)
            {
                startRegion = cartographer.GetRegion(start);
                start.RegionIndex = startRegion.index;
            }
            else
                startRegion = cartographer.regions[start.RegionIndex];
            
            IBaseRegion finishRegion = cartographer.GetRegion(finish);
            finish.RegionIndex = finishRegion.index;

            if(startRegion != finishRegion)
            {
                startRegion.distance = 0;
                SimplePriorityQueue<IBaseRegion, float> regs = new SimplePriorityQueue<IBaseRegion, float>();
                Dictionary<int, IBaseRegion> visited = new Dictionary<int, IBaseRegion>();

                regs.Enqueue(startRegion, Vector3.Distance(startRegion.GetCenter(), finishRegion.GetCenter()));
                while (regs.Count != 0)
                {
                    IBaseRegion cur = regs.Dequeue();
                    if (cur == finishRegion) break;
                    foreach (var reg in cur.Neighbors)
                    {
                        if (!visited.ContainsKey(reg.index))
                        {
                            if (cur.distance + Vector3.Distance(cur.GetCenter(), reg.GetCenter()) < reg.distance)
                            {
                                if (regs.Contains(reg)) regs.Remove(reg);
                                reg.distance = cur.distance + Vector3.Distance(cur.GetCenter(), reg.GetCenter());
                                reg.parent = cur;
                                regs.Enqueue(reg, reg.distance + Vector3.Distance(reg.GetCenter(), finishRegion.GetCenter()));
                            }
                        }
                    }
                    visited.Add(cur.index, cur);
                }
                IBaseRegion nextReg = finishRegion;
                while (nextReg.parent != startRegion)
                {
                    nextReg = nextReg.parent;
                }
                if (nextReg.index != finishRegion.index)
                {
                    Vector3 regCenter = nextReg.GetCenter();
                    regCenter.y = start.Position.y;
                    PathNode targetPoint = new PathNode(regCenter, Vector3.zero);
                    FindPath(start, targetPoint, nextReg.index, movementProperties, updater, 
                        (curPathNode, finPathNode) => cartographer.IsInRegion(curPathNode, nextReg.index));
                    return true;
                }
            }

            finish.Position = new Vector3(finish.Position.x, start.Position.y, finish.Position.z);
            FindPath(start, finish, finish.RegionIndex, movementProperties, updater, (curPathNode, finPathNode) => Vector3.Distance(curPathNode.Position, finPathNode.Position) <= movementProperties.epsilon);
            return true;
        }

        //// Start is called before the first frame update
        void Start()
        {
            //  Инициализируем картографа, ну и всё вроде бы
            cartographer = new Cartographer(CollidersCollection);
            obstaclesLayerMask = 1 << LayerMask.NameToLayer("Obstacles");
            var rend = GetComponent<Renderer>();
            if (rend != null)
                rayRadius = rend.bounds.size.y / 2.5f;
        }
    }
}