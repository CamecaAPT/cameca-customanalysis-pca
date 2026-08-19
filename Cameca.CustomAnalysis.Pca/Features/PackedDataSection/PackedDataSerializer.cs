using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Cameca.CustomAnalysis.Pca;

internal static class PackedDataSerializer
{
    public static void Write<TExtraData>(IIonData ionData, string sectionName, PackedDataDefinition<TExtraData> packedData) where TExtraData : class
    {
        var (manifest, buffer) = Build(packedData);
        ionData.DeleteSection(sectionName);
        ionData.AddSection(AddSectionContext.CreateUnrelated<byte>(sectionName, buffer.Length));
        var extraData = new PackedExtraData<TExtraData>(manifest, packedData.ExtraData);
        var jsonExtraData = JsonSerializer.Serialize(extraData);
        var jsonExtraDataBytes = Encoding.UTF8.GetBytes(jsonExtraData);
        ionData.Sections[sectionName].UpdateExtraData(jsonExtraDataBytes);
        ionData.WriteSection(sectionName, buffer);
    }

    public static PackedDataDefinition<TExtraData>? Read<TExtraData>(IIonData ionData, string sectionName) where TExtraData : class
    {
        if (!IonDataHasSection(ionData, sectionName))
        {
            return null;
        }
        if (!TryGetPinnedHandle(ionData, sectionName, out var handle))
        {
            return null;
        }

        var packedExtraData = JsonSerializer.Deserialize<PackedExtraData<TExtraData>>(ionData.Sections[sectionName].ExtraData);
        // Extract the manifest and extra data
        if (packedExtraData is not { Manifest: { } manifest, ExtraData: TExtraData })
        {
            return null;
        }
        var extraData = packedExtraData.ExtraData;

        // Parse out data
        var packedData = new Dictionary<string, IPackedData>();
        foreach (var (key, info) in packedExtraData.Manifest)
        {
            packedData[key] = new ReadPackedData(info, handle);
        }

        return new PackedDataDefinition<TExtraData>(extraData, packedData);
    }

    private static bool IonDataHasSection(IIonData ionData, string sectionName)
    {
        IReadOnlyDictionary<string, ISectionInfo> sections = ionData.Sections;
        return sections.Keys.Contains(sectionName);
	}

    private static bool TryGetPinnedHandle(IIonData ionData, string sectionName, out MemoryHandle handle)
    {
        // Shouldn't have to iterated everything when only getting initial pointer.
        // Just getting the first should ensure the entire section is memory mapped
        var enumerator = ionData.CreateSectionDataEnumerator(sectionName);
        if (!enumerator.MoveNext())
        {
            handle = default;
            return false;
        }
        var bytesData = enumerator.Current.ReadSectionData<byte>(sectionName);
        handle = bytesData.Pin();
        return true;
    }

    /// <summary>
    /// Output both the header (extra data) and the buffer at the same time
    /// </summary>
    /// <remarks>
    /// Data is written to an allocated input buffer to avoid unnecessary extra copies.
    /// Extra data is returned at the same time to ensure the manifest in the packed header matches the output data.
    /// </remarks>
    /// <exception cref="ArgumentException"></exception>
    private static (Dictionary<string, PackedDataInfo> Manifest, ReadOnlyMemory<byte> Data) Build<TExtraData>(PackedDataDefinition<TExtraData> definition) where TExtraData : class
    {
        // Resolve to the current required size
        Memory<byte> buffer = new byte[definition.RequiredDataSize];

        var dataManifest = new Dictionary<string, PackedDataInfo>();
        int offset = 0;
        foreach (var (key, item) in definition.PackedData)
        {
            int length = item.LengthBytes;
            dataManifest[key] = new PackedDataInfo(item.DType, item.Shape, new int[] { offset, offset + length }, item.Order);
            var bytes = item.GetDataAsType<byte>();
            bytes.CopyTo(buffer.Span.Slice(offset, length));
            offset += length;
        }
        return (dataManifest, buffer);
    }
}