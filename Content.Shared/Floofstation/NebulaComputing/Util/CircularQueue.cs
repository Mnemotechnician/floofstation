using System.Collections;
using Robust.Shared.Serialization;


namespace Content.Shared.FloofStation.NebulaComputing.Util;


/// <summary>
///     A circular array-based queue with O(1) write and read operations and limited capacity.
///     When the queue is full and a new element is added, the oldest element is overwritten.
/// </summary>
/// <remarks>Useful for things like terminal outputs.</remarks>
[Serializable, NetSerializable]
public class CircularQueue<T> : IEnumerable<T>
{
    private T[] _queue;
    private int _front;
    private int _rear;
    public int Capacity { get; }
    public int Count { get; private set; }

    public CircularQueue(int size)
    {
        _queue = new T[size];
        _front = 0;
        _rear = -1;
        Capacity = size;
        Count = 0;
    }

    /// <summary>
    ///     Add an item to the end of the queue.
    /// </summary>
    public void Enqueue(T item)
    {
        _rear = (_rear + 1) % Capacity; // Circular increment
        _queue[_rear] = item;

        if (IsFull())
            _front = (_front + 1) % Capacity; // Move front to override the oldest element
        else
            Count++;
    }

    /// <summary>
    ///     Remove an item from the beginning of the queue.
    /// </summary>
    public T Dequeue()
    {
        if (IsEmpty())
            throw new InvalidOperationException("Queue is empty");

        T item = _queue[_front];
        _front = (_front + 1) % Capacity; // Circular increment
        Count--;
        return item;
    }

    public T Peek()
    {
        if (IsEmpty())
            throw new InvalidOperationException("Queue is empty");
        return _queue[_front];
    }

    public bool IsEmpty() => Count == 0;

    public bool IsFull() => Count == Capacity;

    public IEnumerator<T> GetEnumerator()
    {
        var index = _front;
        for (var i = 0; i < Count; i++)
        {
            yield return _queue[index];
            index = (index + 1) % Capacity;
        }
    }

    public IEnumerator<T> GetEnumeratorReverse()
    {
        var index = _rear;
        for (var i = 0; i < Count; i++)
        {
            yield return _queue[index];

            index = (index - 1);
            if (index < 0)
                index = Capacity - 1;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class CircularQueueExtensions
{
    public static string ToString(this CircularQueue<char> queue)
    {
        var buffer = new char[queue.Count];
        int index = 0;

        foreach (var c in queue)
            buffer[index++] = c;

        return new string(buffer);
    }

    public static void Append(this CircularQueue<char> queue, string str)
    {
        // This could probably be done better, but eh
        foreach (var c in str)
            queue.Enqueue(c);
    }
}
