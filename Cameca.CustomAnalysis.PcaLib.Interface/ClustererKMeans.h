#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/ClustererKMeans.h"
#include "VectorMarshaller.h"
#include "ClusterData.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {
	public ref class ClustererKMeans : ManagedWrapper<PcaLib::ClustererKMeans>
	{
	public:
		ClustererKMeans(VoxelFeatureMatrix^ matrix)
			: ManagedWrapper(new PcaLib::ClustererKMeans(matrix->GetInstance()->pImpl)) { }
		ClustererKMeans(VoxelFeatureMatrix^ matrix, System::Collections::Generic::IEnumerable<System::Collections::Generic::IEnumerable<float>^>^ loads)
			: ManagedWrapper(new PcaLib::ClustererKMeans(matrix->GetInstance()->pImpl, VectorMarshaller::ToNestedVector(loads))) { }

		~ClustererKMeans() {
			if (matrix != nullptr)
			{
				delete matrix;
				matrix = nullptr;
			}
		}

		ClusterData Cluster(const int nClust, const int nReplicates, const bool weighted)
		{
			auto sClusterData = GetInstance()->Cluster(nClust, nReplicates, weighted);
			return ClusterData(sClusterData.cost, VectorMarshaller::ToArray(sClusterData.voxelIndex));
		}

	private:
		VoxelFeatureMatrix^ matrix;
	};
}
