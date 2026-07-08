#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/GaussianMixtureModel.h"
#include "VectorMarshaller.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {

	[StructLayout(LayoutKind::Sequential, Pack = 1)]
	public value struct GMMResult
	{
	public:
		float Cost;
		initonly cli::array<int>^ VoxelIndex;

		GMMResult(float cost, cli::array<int>^ voxelIndex)
			: Cost(cost), VoxelIndex(voxelIndex) {
		}
	};

	public ref class GaussianMixtureModel : ManagedWrapper<PcaLib::GaussianMixtureModel>
	{
	public:
		GaussianMixtureModel(VoxelFeatureMatrix^ matrix)
			: ManagedWrapper(new PcaLib::GaussianMixtureModel(matrix->GetInstance()->pImpl))
		{
		}

		~GaussianMixtureModel() {
			if (matrix != nullptr)
			{
				delete matrix;
				matrix = nullptr;
			}
		}

		GMMResult TrainModel(const int nClust, const int nReplicates, const float regParam)
		{
			auto sClusterData = GetInstance()->TrainModel(nClust, nReplicates, regParam);
			return GMMResult(sClusterData.cost, VectorMarshaller::ToArray(sClusterData.voxelIndex));
		}

	private:
		VoxelFeatureMatrix^ matrix;
	};
}
