namespace Cameca.CustomAnalysis.Pca;


public interface IGridsUsageDelegate
{
    void UseGridForPca(string gridID, bool useIt);
    bool UsesGridForPca(string gridID);
    string GridInfo(string gridID);
}

public class DoNothingGridsUsageDelegate : IGridsUsageDelegate
{
    public DoNothingGridsUsageDelegate()
    {
    }

    public void UseGridForPca(string gridID, bool useIt)
    {

    }

    public bool UsesGridForPca(string gridID)
    {
        return true;
    }

    public string GridInfo(string gridID)
    {
        return "No info about grid: " + gridID;
    }
}