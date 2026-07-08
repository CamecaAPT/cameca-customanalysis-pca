#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/MultinomialMixtureModel.h"
#include "VectorMarshaller.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {

	[StructLayout(LayoutKind::Sequential, Pack = 1)]
	public value struct MnMMResult
	{
	public:
		float Cost;
		initonly cli::array<int>^ VoxelIndex;

		MnMMResult(float cost, cli::array<int>^ voxelIndex)
			: Cost(cost), VoxelIndex(voxelIndex) {
		}
	};

	public ref class MultinomialMixtureModel : ManagedWrapper<PcaLib::MultinomialMixtureModel>
	{
	public:
		MultinomialMixtureModel(VoxelFeatureMatrix^ matrix)
			: ManagedWrapper(new PcaLib::MultinomialMixtureModel(matrix->GetInstance()->pImpl))
		{
		}

		~MultinomialMixtureModel() {
			if (matrix != nullptr)
			{
				delete matrix;
				matrix = nullptr;
			}
		}

		MnMMResult TrainModel(const int nClust, const int nReplicates)
		{
			auto sClusterData = GetInstance()->TrainModel(nClust, nReplicates);
			return MnMMResult(sClusterData.cost, VectorMarshaller::ToArray(sClusterData.voxelIndex));
		}

	private:
		VoxelFeatureMatrix^ matrix;
	};
}
