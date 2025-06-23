using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Floof.NebulaComputing.Util;
using NUnit.Framework;


namespace Content.Tests.Server._Floof.NebulaComputing;


[TestFixture]
[TestOf(typeof(CircularQueue<>))]
public sealed class CircularQueueTest
{
    [Test]
    public void TestSimpleQueue()
    {
        var queue = new IntCircularQueue(3);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);

        Assert.That(queue.Peek() == 1);
        Assert.That(queue.ToArray(), Is.EqualTo((int[]) [1, 2, 3]));
        Assert.That(queue.GetEnumeratorReverse().ToArray(), Is.EqualTo((int[]) [3, 2, 1]));

        queue.Enqueue(4);
        Assert.That(queue.Peek() == 2);
        Assert.That(queue.ToArray(), Is.EqualTo((int[]) [2, 3, 4]));
        Assert.That(queue.GetEnumeratorReverse().ToArray(), Is.EqualTo((int[]) [4, 3, 2]));

        queue.Enqueue(5);
        Assert.That(queue.Peek() == 3);
        Assert.That(queue.ToArray(), Is.EqualTo((int[]) [3, 4, 5]));
        Assert.That(queue.GetEnumeratorReverse().ToArray(), Is.EqualTo((int[]) [5, 4, 3]));
    }

    [Test]
    public void TestObjectQueue()
    {
        object objA = new object(), objB = "abc", objC = new object(), objD = this;

        var queue = new ObjectCircularQueue(3);
        queue.Enqueue(objA);
        queue.Enqueue(objB);
        queue.Enqueue(objC);
        queue.Enqueue(objD);

        Assert.That(queue.Count, Is.EqualTo(3));
        Assert.That(queue.Dequeue(), Is.EqualTo("abc"));
        Assert.That(queue.Dequeue(), Is.EqualTo(objC));
        Assert.That(queue.Dequeue(), Is.EqualTo(objD));
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
        Assert.That(queue.IsEmpty, Is.True);
    }
}

internal static class Extensions
{
    public static T[] ToArray<T>(this IEnumerator<T> enumerator)
    {
        var list = new List<T>();
        while (enumerator.MoveNext())
            list.Add(enumerator.Current);
        return list.ToArray();
    }
}
