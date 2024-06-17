using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Agent2Script : MonoBehaviour
{
    private NavMeshAgent agent;
    private Vector3? target;
    public float UpdateInterval = 0.1f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        InvokeRepeating(nameof(SetDestination), 0.0f, 1.0f);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                target = hit.point;
            }
        }
    }

    void SetDestination()
    {
        if (target.HasValue)
        {
            NavMeshPath path = new NavMeshPath();
            agent.CalculatePath(target.Value, path);
            agent.SetPath(path);
        }
    }
}
