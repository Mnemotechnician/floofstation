using System.Runtime.CompilerServices;


namespace Content.Server.FloofStation.NebulaComputing.VirtualCPU.Util;


public static class ConcurrencyUtils
{
    /// <summary>
    ///     Clears a list and returns its original contents as a copy. Locks on the list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] CopyAndClear<T>(ref List<T> list)
    {
        lock (list)
        {
            var copy = list.ToArray();
            list.Clear();
            return copy;
        }
    }

    /// <summary>
    ///     Locks the list, copies it, and returns the copy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Copy<T>(ref List<T> list)
    {
        lock (list)
            return list.ToArray();
    }

    /// <summary>
    ///     Locks the list and clears it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear<T>(ref List<T> list)
    {
        lock (list)
            list.Clear();
    }
}
