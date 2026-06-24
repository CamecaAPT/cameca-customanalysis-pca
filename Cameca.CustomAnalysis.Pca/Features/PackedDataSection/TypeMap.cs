using System;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

internal static class TypeMap
{
    private static readonly Dictionary<Type, string> DTypeMap = new()
{
    { typeof(sbyte),  "|i1" },
    { typeof(byte),   "|u1" },
    { typeof(short),  "<i2" },
    { typeof(ushort), "<u2" },
    { typeof(int),    "<i4" },
    { typeof(uint),   "<u4" },
    { typeof(long),   "<i8" },
    { typeof(ulong),  "<u8" },
    { typeof(Half),   "<f2" },
    { typeof(float),  "<f4" },
    { typeof(double), "<f8" }
};

    public static string DType(Type type)
    {
        if (DTypeMap.TryGetValue(type, out var dtype))
        {
            return dtype;
        }
        throw new NotSupportedException($"Serialization of type [{type.Name}] is not supported. [Booleans should be stored as bytes, Char as ushort. Platform dependant sized types (nint/nuint) should be pinned to a concrete width for reproducibitly. Other types are currently out of scope]");
    }

    public static string DType<T>() => DType(typeof(T));

    public static Type SystemType(string dtype)
    {
        foreach (var (key, value) in DTypeMap)
        {
            if (string.Equals(value, dtype, StringComparison.Ordinal))
            {
                return key;
            }
        }
        throw new NotSupportedException($"The dtype string '{dtype}' is not supported or recognized.");
    }
}
