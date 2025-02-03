using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using CommunityToolkit.HighPerformance.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(PcaViewModel.UniqueId, typeof(PcaViewModel))]
[INotifyPropertyChanged]
internal partial class PcaNode : AnalysisFilterNodeBase
{
    private readonly ResourceFactory resourceFactory;
    private readonly INodeDataProvider nodeDataProvider;

    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaNode";
    
    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Principal Component Analysis");

    [ObservableProperty]
    private EigenvalueResults? eigenvalueResults;

    [ObservableProperty]
    private ComponentsResults? componentsResults;

    // Properties is always created and not null after on-created: the only time this would be null should be with an attempt to access from the ctor, which should not be attempted
    public PcaOptions Options => (PcaOptions)Properties!;

    public Guid Id => InstanceId;

    public PcaNode(
        IAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory,
        INodeDataProvider nodeDataProvider)
        : base(services)
    {
        this.resourceFactory = resourceFactory;
        this.nodeDataProvider = nodeDataProvider;
    }

    private bool invalidateResults = true;
    protected override void OnDataIsValidChanged(bool isValid)
    {
        if (!isValid && invalidateResults)
        {
            InvalidateAll();
        }
        invalidateResults = true;
    }

    internal async Task RunPcaFromGrid3D()
    {
        int nComponents = Options.Components;
        IsError = false;
        var resources = resourceFactory.CreateResource(InstanceId);
        if (await resources.GetIonData() is not { } ionData)
        {
            throw new InvalidOperationException("No IIonData");
        }

        INodeResource gridNode;
        try
        {
            // Throws InvalidOperationException if not able to resolve a single correct Grid3D node
            gridNode = resources.FindGridNode(nodeDataProvider);
        }
        catch (InvalidOperationException)
        {
            IsError = true;
            return;
        }

        if (await nodeDataProvider.Resolve(gridNode.Id)!.GetData(typeof(IGrid3DData)) is not IGrid3DData gridData)
        {
            throw new InvalidOperationException("No IGrid3DData");
        }

        ulong rangedCount = ionData.GetIonTypeCounts().Values.Sum();
        int nIons = (int)rangedCount;

        // Remove empty voxels
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];
        int nFeatures = ionData.Ions.Count;

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => gridData.GetDataForIon(ionIndex).ToArray())
            .ToArray();

        var nonEmptyVoxels = new List<int>();
        for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
        {
            for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
            {
                if (localBuffer[ionIndex][voxelIndex] != 0f)
                {
                    nonEmptyVoxels.Add(voxelIndex);
                    break;
                }
            }
        }
        int nVoxels = nonEmptyVoxels.Count;

        var dataBuffer = new float[nFeatures * nVoxels];
        for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
        {
            int x = 0;
            foreach (int voxelIndex in nonEmptyVoxels)
            {
                dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
            }
        }

        int nevals = nFeatures;  // Input?


        // Allocated output buffers
        float[] scores = new float[nVoxels * nComponents];
        float[] loads = new float[nFeatures * nComponents];
        float[] evals = new float[nevals];

        // Call the doPCA function
        PcaLib.doPCA(nVoxels, nFeatures, dataBuffer, nIons, nComponents, nevals, scores, loads, evals);

        // Normalization
        for (int i = 0; i < scores.Length; i++)
        {
            scores[i] *= 1000f;
        }
        for (int i = 0; i < loads.Length; i++)
        {
            loads[i] /= 1000f;
        }

        if (EigenvalueResults is null)
        {
            EigenvalueResults = new EigenvalueResults(evals);
        }
        
        var components = new List<ComponentResults>(nComponents);
        for (int i = 0; i < nComponents; i++)
        {
            var compScores = scores.Skip(i * nVoxels).Take(nVoxels).ToArray();
            var compLoads = loads.Skip(i * nFeatures).Take(nFeatures).ToArray();
            components.Add(new ComponentResults(compScores, compLoads));
        }
        ComponentsResults = new ComponentsResults(nonEmptyVoxels.ToArray(), components);

        UpdateOptionsBounds();
    }

    private void UpdateOptionsBounds()
    {
        var selectedResult = ComponentsResults?.Components.ElementAtOrDefault(Options.ComponentIndex);
        Options.Min = selectedResult?.Scores.Min();
        Options.Max = selectedResult?.Scores.Max();
    }

    private bool IsError
    {
        get => DataState is { } dataState && dataState.IsErrorState;
        set
        {
            if (DataState is { } dataState)
            {
                dataState.IsErrorState = value;
            }
        }
    }

    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(IIonData ionData, IProgress<double>? progress, [EnumeratorCancellation] CancellationToken token)
    {
        IsError = false;
        int nComponents = Options.Components;
        var resources = resourceFactory.CreateResource(InstanceId);

        INodeResource gridNode;
        try
        {
            // Throws InvalidOperationException if not able to resolve a single correct Grid3D node
            gridNode = resources.FindGridNode(nodeDataProvider);
        }
        catch (InvalidOperationException)
        {
            IsError = true;
            DataStateIsValid = false;
            yield break;
        }

        if (await nodeDataProvider.Resolve(gridNode.Id)!.GetData(typeof(IGrid3DData)) is not IGrid3DData gridData)
        {
            DataStateIsValid = false;
            throw new InvalidOperationException("No IGrid3DData");
        }

        ulong rangedCount = ionData.GetIonTypeCounts().Values.Sum();
        int nIons = (int)rangedCount;

        // Remove empty voxels
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];
        int nFeatures = ionData.Ions.Count;

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => gridData.GetDataForIon(ionIndex).ToArray())
            .ToArray();

        var nonEmptyVoxels = new List<int>();
        for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
        {
            for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
            {
                if (localBuffer[ionIndex][voxelIndex] != 0f)
                {
                    nonEmptyVoxels.Add(voxelIndex);
                    break;
                }
            }
        }
        int nVoxels = nonEmptyVoxels.Count;

        var dataBuffer = new float[nFeatures * nVoxels];
        for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
        {
            int x = 0;
            foreach (int voxelIndex in nonEmptyVoxels)
            {
                dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
            }
        }

        int nevals = nFeatures;  // Input?


        // Allocated output buffers
        float[] scores = new float[nVoxels * nComponents];
        float[] loads = new float[nFeatures * nComponents];
        float[] evals = new float[nevals];

        // Call the doPCA function
        PcaLib.doPCA(nVoxels, nFeatures, dataBuffer, nIons, nComponents, nevals, scores, loads, evals);

        var minVector = new Vector3(
            (float)gridData.GridRange[0, 0],
            (float)gridData.GridRange[1, 0],
            (float)gridData.GridRange[2, 0]);
        var voxelSize = new Vector3(
        (float)gridData.VoxelSize[0],
            (float)gridData.VoxelSize[1],
            (float)gridData.VoxelSize[2]);
        var binner = new Binner(minVector, voxelSize, gridData.NumVoxels[0], gridData.NumVoxels[1]);

        var scoredVoxels = nonEmptyVoxels
            .Zip(scores.Skip(Options.ComponentIndex * nonEmptyVoxels.Count).Select(x => x * 1000f))
            .ToDictionary(x => x.First, x => x.Second);

        ulong index = 0ul;
        float threshold = Options.Isovalue;
        foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position))
        {
            int bufferIndex = 0;
            using var buffer = MemoryOwner<ulong>.Allocate(chunk.Length);
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            for (int chunkIndex = 0; chunkIndex < chunk.Length; chunkIndex++)
            {
                var bin = binner.ToBin(positions.Span[chunkIndex]);
                if (scoredVoxels.TryGetValue(bin, out float score) && score >= threshold)
                {
                    buffer.Span[bufferIndex++] = index;
                }
                index += 1ul;
            }
            yield return buffer.Slice(0, bufferIndex).Memory;
        }

        DataStateIsValid = true;
    }

    #region Save / Load Options
    protected override byte[]? GetSaveContent()
    {
        var serializer = new XmlSerializer(typeof(PcaOptions));
        using var stringWriter = new StringWriter();
        serializer.Serialize(stringWriter, Properties);
        return Encoding.UTF8.GetBytes(stringWriter.ToString());
    }

    protected override void OnCreated(NodeCreatedEventArgs eventArgs)
    {
        base.OnCreated(eventArgs);

        // If loading existing and data is present, populate properties from serialized data
        if (eventArgs.Trigger == EventTrigger.Load && eventArgs.Data is { } data)
        {
            var xmlData = Encoding.UTF8.GetString(data);
            var serializer = new XmlSerializer(typeof(PcaOptions));
            using var stringReader = new StringReader(xmlData);
            if (serializer.Deserialize(stringReader) is PcaOptions loadedOptions)
            {
                Properties = loadedOptions;
            }
        }
        if (Properties is null)
        {
            Properties = new PcaOptions();
        }

        // Wire up change notification events
        Properties = Properties is PcaOptions typedProperties ? typedProperties : new();
        if (Properties is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged += PropertiesObjectOnPropertyChanged;
        }
    }
    #endregion

    private void PropertiesObjectOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        CanSave = true;
        switch (e.PropertyName)
        {
            case nameof(PcaOptions.Components):
                InvalidateComponents();
                break;
            case nameof(PcaOptions.ComponentIndex):
                UpdateOptionsBounds();
                MarkInvalidKeepResults();
                break;
            case nameof(PcaOptions.Isovalue):
                MarkInvalidKeepResults();
                break;
            case nameof(PcaOptions.Invert):
                FilterIsInverted = Options.Invert;
                MarkInvalidKeepResults();
                break;
            default:
                break;
        }
    }

    private void MarkInvalidKeepResults()
    {
        invalidateResults = false;
        DataStateIsValid = false;
    }

    private void InvalidateAll()
    {
        EigenvalueResults = null;
        InvalidateComponents();
    }

    private void InvalidateComponents()
    {
        ComponentsResults = null;
    }
}
