#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/NegMultinomialMixtureModel.h"
#include "VectorMarshaller.h"
#include "ClusterData.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {

	[StructLayout(LayoutKind::Sequential, Pack = 1)]
	public value struct NegMnMMResult
	{
	public:
		float Cost;
		initonly cli::array<int>^ VoxelIndex;

		NegMnMMResult(float cost, cli::array<int>^ voxelIndex)
			: Cost(cost), VoxelIndex(voxelIndex) {
		}
	};

	public ref class NegMultinomialMixtureModel : ManagedWrapper<PcaLib::NegMultinomialMixtureModel>
	{
	public:
		NegMultinomialMixtureModel(VoxelFeatureMatrix^ matrix)
			: ManagedWrapper(new PcaLib::NegMultinomialMixtureModel(matrix->GetInstance()->pImpl))
		{
		}

		~NegMultinomialMixtureModel() {
			if (matrix != nullptr)
			{
				delete matrix;
				matrix = nullptr;
			}
		}

		NegMnMMResult TrainModel(const int nClust, const int nReplicates)
		{
			auto sClusterData = GetInstance()->TrainModel(nClust, nReplicates);
			return NegMnMMResult(sClusterData.cost, VectorMarshaller::ToArray(sClusterData.voxelIndex));
		}

	private:
		VoxelFeatureMatrix^ matrix;
	};
}
