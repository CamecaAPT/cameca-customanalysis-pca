#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/OrthNonNegMatrixFactorization.h"
#include "VectorMarshaller.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {

	[StructLayout(LayoutKind::Sequential, Pack = 1)]
	public value struct ONMFResults
	{
	public:
		float Cost;
		initonly cli::array<int>^ VoxelIndex;

		ONMFResults(float cost, cli::array<int>^ voxelIndex)
			: Cost(cost), VoxelIndex(voxelIndex) {
		}
	};

	public ref class OrthNonNegMatrixFactorization : ManagedWrapper<PcaLib::OrthNonNegMatrixFactorization>
	{
	public:
		OrthNonNegMatrixFactorization(VoxelFeatureMatrix^ matrix)
			: ManagedWrapper(new PcaLib::OrthNonNegMatrixFactorization(matrix->GetInstance()->pImpl))
		{
		}

		~OrthNonNegMatrixFactorization() {
			if (matrix != nullptr)
			{
				delete matrix;
				matrix = nullptr;
			}
		}

		ONMFResults TrainModel(const int nClust, const int nReplicates, const bool weighted)
		{
			auto sClusterData = GetInstance()->TrainModel(nClust, nReplicates, weighted);
			return ONMFResults(sClusterData.cost, VectorMarshaller::ToArray(sClusterData.voxelIndex));
		}

	private:
		VoxelFeatureMatrix^ matrix;
	};
}
