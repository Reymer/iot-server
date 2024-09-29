using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> executionQueue = new Queue<Action>();

    private static UnityMainThreadDispatcher instance = null;

    public static UnityMainThreadDispatcher Instance()
    {
        if (!instance)
        {
            throw new Exception("UnityMainThreadDispatcher is not initialized. Please add it to a GameObject in the scene.");
        }
        return instance;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    public static void Initialize()
    {
        if (instance == null)
        {
            GameObject obj = new("UnityMainThreadDispatcher");
            instance = obj.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(obj);
        }
    }
}
