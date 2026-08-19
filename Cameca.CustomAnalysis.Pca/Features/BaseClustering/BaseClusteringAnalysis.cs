using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.CustomAnalysis.Utilities.Segmentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

internal abstract partial class BaseClusteringAnalysis<TProperties> : BasicCustomAnalysisBase<TProperties>
    where TProperties : BaseClusteringProperties, new()
{

    protected readonly SegmentedRoiManager<IStandardAnalysisFilterNodeBaseServices> segmentedManager;

    [ObservableProperty]
    private ICollection<IRenderData> itemsSource = Array.Empty<IRenderData>();

    [ObservableProperty]
    private string ticPlotXAxisLabel = "X";

    [ObservableProperty]
    private IReadOnlyDictionary<string, string> nameMap = new Dictionary<string, string>();

    private string CreateTicPlotXAxisLabel(double voxelSize) => $"Total Ions / {voxelSize:0.##} nm³";

    protected BaseClusteringAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory)
        : base(services, resourceFactory)
    {
        segmentedManager = SegmentedRoiManager.Create(this);
        Resources.Events.SubscribeRenameNode(HandleChildRename, FilterChildRename);
    }

    protected override void OnAdded(NodeAddedEventArgs eventArgs)
    {
        base.OnAdded(eventArgs);
        if (eventArgs.Trigger == EventTrigger.Create)
        {
            SyncPlaceholderExpectedSegments(Properties.ClusterCount);
        }
    }

    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertiesChanged(e);
        CanSave = true;
        // Doesn't invalidate
        if (e.PropertyName == nameof(Properties.TicBinWidth))
        {
            SetHistogramCustomSteps();
        }
        // Does in validate
        else
        {
            DataStateIsValid = false;
            if (e.PropertyName == nameof(Properties.ClusterCount))
            {
                SyncPlaceholderExpectedSegments(Properties.ClusterCount);
            }
        }
    }

    // Returns an int array of assignments to each sparse voxel (should match same length as sparse voxel index map)
    protected abstract int[] Cluster(IIonData ionData, VoxelFeatureMatrixSectionData voxelFeatureMatrixSectionData);

    protected void RunCluster(IIonData ionData)
    {
        if (DataStateIsValid && !DataStateIsError)
        {
            return;
        }
        var data = ionData.GetNullableVoxelFeatureMatrixSectionData(Resources.Parent!.DataSectionName);
        if (data is null || data.VoxelFeatureMatrix.DataLength == 0)
        {
            return;
        }

        var existingResults = IonAssignmentSerializer.ReadFromIonDataSection(ionData, Resources.DataSectionName);
        var sanitizedResults = existingResults ?? CalculateClustering(ionData, data);

        CreateTicPlots(data, sanitizedResults);

        // Created new, needs to be saved
        if (existingResults is null)
        {
            IonAssignmentSerializer.WriteToIonDataSection(
                ionData,
                Resources.DataSectionName,
                data.ExtraData.GridParameters,
                data.VoxelFeatureMatrix.GetVoxelIndices(),
                sanitizedResults);
        }
        UpdateSegmentedChildrenRois();
        DataStateIsValid = true;
    }

    private byte[] CalculateClustering(IIonData ionData, VoxelFeatureMatrixSectionData data)
    {
        int clusters = Properties.ClusterCount;

        double voxelSize = data.ExtraData.GridParameters.VoxelSize;
        TicPlotXAxisLabel = CreateTicPlotXAxisLabel(voxelSize);
        if (lastVoxelSize != voxelSize)
        {
            SetClampedTicWidth((int)Math.Pow(Math.Round(voxelSize), 3));
            lastVoxelSize = voxelSize;
        }
        var clusterResults = Cluster(ionData, data);

        // If for whatever reason, there are more assignments than clusteres, eliminated extras (floating point errors?)
        // and represent them as ignored byte.MaxValue
        var sanitizedResults = new byte[clusterResults.Length];
        for (int i = 0; i < clusterResults.Length; ++i)
        {
            int raw = clusterResults[i];
            byte sanitized = raw < clusters ? (byte)raw : byte.MaxValue;
            sanitizedResults[i] = sanitized;
        }
        return sanitizedResults;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        IIonData ownerIonData,
        IProgress<double>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        RunCluster(ownerIonData);
        foreach (var chunk in ownerIonData.AllowAllFilter(progress, token))
        {
            yield return chunk;
        }
    }

    protected override async Task<bool> Update(CancellationToken cancellationToken)
    {
        if (await Resources.GetIonData(null, cancellationToken) is { } ionData)
        {
            RunCluster(ionData);
            return true;
        }
        return false;
    }

    protected void UpdateSegmentedChildrenRois()
    {
        segmentedManager.UpdateSync(
            getSegmentTitle: GetSegmentTitle,
            excludeIds: new[] { byte.MaxValue },
            cancellationToken: default);
    }

    protected virtual string GetSegmentTitle(byte id)
    {
        foreach (var child in Resources.Children)
        {
            if (Services.InstanceProvider.Resolve(child.Id) is SegmentedRoiNode roiNode
                && roiNode.Properties.FilterValue == id
                && Services.NodeInfoProvider.Resolve(child.Id) is { } nodeInfo)
            {
                return nodeInfo.Name;
            }
        }

        return $"Cluster {id + 1}";
    }

    private void SyncPlaceholderExpectedSegments(int segmentCount)
    {
        var segmentationValues = Enumerable.Range(0, segmentCount).Select(i => (byte)i).ToHashSet();

        foreach (var child in Resources.Children)
        {
            if (Services.InstanceProvider.Resolve(child.Id) is not SegmentedRoiNode roiNode)
            {
                continue;
            }

            // pop from the copy so we don't consider again
            if (segmentationValues.Contains(roiNode.Properties.FilterValue))
            {
                segmentationValues.Remove(roiNode.Properties.FilterValue);
            }
            // the child has a filter ID that is not in the expected list: just ignore it
        }
        // for everything remaining in the copy at this point, it didn't match so it needs to be created
        foreach (var newFilterId in segmentationValues)
        {
            var title = GetSegmentTitle(newFilterId);
            var newId = Resources.CreateChildNode(SegmentedRoiNode.UniqueId, Id, title);
            if (Services.InstanceProvider.Resolve(newId) is SegmentedRoiNode childFilterNode)
            {
                childFilterNode.Properties.FilterValue = newFilterId;
            }
        }
    }
    private void HandleChildRename(RenameNodeEventArgs e)
    {
        if (Services.InstanceProvider.Resolve(e.NodeId) is SegmentedRoiNode roiNode)
        {
            string key = $"Cluster {roiNode.Properties.FilterValue + 1}";
            var updated = new Dictionary<string, string>(NameMap);
            updated[key] = e.Name;
            NameMap = updated;
        }
    }

    private bool FilterChildRename(RenameNodeEventArgs e) => Resources.Children.Any(x => x.Id == e.NodeId);


    [RelayCommand]
    public void Rebin(MouseWheelEventArgs e)
    {
        // This should be in the view, but we can clean it up later (same with the EventArgs should be abstracted)
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            e.Handled = true;
            int steps = -(e.Delta / 120);
            // Ensure always minimum of 1
            int newWidth = Math.Max(Properties.TicBinWidth + steps, 1);
            SetClampedTicWidth(newWidth);
        }
    }

    protected override void OnDataIsValidChanged(bool isValid)
    {
        base.OnDataIsValidChanged(isValid);
        if (!isValid)
        {
            baseBinnedData = null;
            Resources.TopLevelNode.GetValidIonData()!.DeleteSection(Resources.DataSectionName);
        }
    }

    private double lastVoxelSize = 0f;
    private List<Vector2[]>? baseBinnedData = null;

    private void CreateTicPlots(VoxelFeatureMatrixSectionData data, byte[] results)
    {
        var voxelSize = data.ExtraData.GridParameters.VoxelSize;
        TicPlotXAxisLabel = CreateTicPlotXAxisLabel(voxelSize);
        if (lastVoxelSize != voxelSize)
        {
            SetClampedTicWidth((int)Math.Pow(Math.Round(voxelSize), 3));
            lastVoxelSize = voxelSize;
        }


        var voxelTic = new float[data.VoxelFeatureMatrix.VoxelCount];
        for (int i = 0; i < data.VoxelFeatureMatrix.FeatureCount; i++)
        {
            var featureData = data.VoxelFeatureMatrix.GetFeatureData(i);
            for (int j = 0; j < data.VoxelFeatureMatrix.VoxelCount; j++)
            {
                voxelTic[j] += featureData.Span[j];
            }
        }
        float minVoxelTick = voxelTic.Min();
        var minBin = (int)MathF.Floor(minVoxelTick);

        var clusterIncludedVoxelCount = Enumerable.Repeat(0, Properties.ClusterCount).ToList();
        var clusterHistData = Enumerable.Range(0, Properties.ClusterCount).Select(_ => new List<int>()).ToList();

        for (int i = 0; i < data.VoxelFeatureMatrix.VoxelCount; i++)
        {
            byte assignment = results[i];
            if (assignment != byte.MaxValue)
            {
                // select the correct histogram
                var assignmentHist = clusterHistData[assignment];
                // consider x-axis to be bins of width 1 (integer TIC values). So pad out length if the value is larger than length
                float tic = voxelTic[i];
                int floorTic = (int)Math.Floor(tic);
                int bin = floorTic - minBin;
                if (assignmentHist.Count() < bin + 1)
                {
                    assignmentHist.AddRange(Enumerable.Repeat(0, bin + 1 - assignmentHist.Count()));
                }
                assignmentHist[bin] += 1;
                clusterIncludedVoxelCount[assignment] += 1;
            }
        }

        // Normalize to PDF and convert to historgram Vector2 values
        baseBinnedData = new List<Vector2[]>();
        var renderDataValues = Enumerable.Repeat(new List<Vector2>(), Properties.ClusterCount).ToList();
        for (int i = 0; i < Properties.ClusterCount; i++)
        {
            var assignmentHist = clusterHistData[i];
            int totalCount = clusterIncludedVoxelCount[i];
            var clusterRenderDataValues = new Vector2[assignmentHist.Count];
            if (totalCount > 0)
            {
                for (int j = 0; j < assignmentHist.Count(); j++)
                {
                    clusterRenderDataValues[j] = new Vector2(j + minBin, assignmentHist[j] / (float)totalCount);
                }
            }

            baseBinnedData.Add(clusterRenderDataValues.ToArray());
        }

        var newTics = new List<IRenderData>();
        foreach (var (d, i) in baseBinnedData.Select((data, i) => (data, i)))
        {
            var histRenderData = Resources.ChartObjects.CreateHistogram(
                d,
                color: Colors.Blue,
                customStep: Properties.TicBinWidth,
                name: $"Cluster {i + 1}");
            newTics.Add(histRenderData);
        }

        ItemsSource = newTics;
    }

    private void SetHistogramCustomSteps()
    {
        if (ItemsSource is { Count: > 0 })
        {
            foreach (var x in ItemsSource)
            {
                if (x is IHistogramRenderData histData)
                {
                    histData.CustomStep = Properties.TicBinWidth;
                }

            }
        }
    }

    private void SetClampedTicWidth(int newWidth)
    {
        Properties.TicBinWidth = Math.Max(newWidth, 1);
    }
}
