#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/PrincipalComponentAnalysis.h"
#include "VectorMarshaller.h"
#include "ComponentData.h"
#include "GridParameters.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {
	public ref class PrincipalComponentAnalysis : ManagedWrapper<PcaLib::PrincipalComponentAnalysis>
	{
	public:
		PrincipalComponentAnalysis(GridParameters^ gridParams, VoxelFeatureMatrix^ matrix)
			: ManagedWrapper(new PcaLib::PrincipalComponentAnalysis(matrix->GetInstance()->pImpl))
			, matrix(matrix)
			, gridParams(gridParams)
		{
		}

		~PrincipalComponentAnalysis() {
			if (matrix != nullptr)
			{
				delete matrix;
				matrix = nullptr;
			}
		}

		cli::array<float>^ GetEigenvalues()
		{
			auto result = GetInstance()->GetEigenvalues();
			if (result.info > 0) {
				throw gcnew System::Exception("The iterative algorithm for computing eigenvalues or eigenvectors fails to converge with the set parameters in the permitted number of iterations. Different inputs, specifically reducing the number of voxels or features, may help. If this issue is encountered with reasonable numbers of voxels and features, please report the issue with repro steps.");
			}

			return VectorMarshaller::ToArray(result.value);
		}

		int EstimateRank(const int gaps, const int significance, const bool refine)
		{
			return GetInstance()->EstimateRankF(gaps, significance, refine);
		}

		cli::array<float>^ GetNoiseEigenvalues(const int rank)
		{
			auto result = GetInstance()->GetNoiseEigenvalues(rank);

			return VectorMarshaller::ToArray(result);
		}

		cli::array<ComponentData>^ GetComponents(const int nComponents)
		{
			auto result = GetInstance()->GetComponents(nComponents);
			if (result.info > 0) {
				throw gcnew System::Exception("The iterative algorithm for computing eigenvalues or eigenvectors fails to converge with the set parameters in the permitted number of iterations. Different inputs, specifically reducing the number of voxels or features, may help correct this issue.");
			}
			auto nativeData = result.value;
			auto outputArray = gcnew cli::array<ComponentData>(nativeData.size());
			for (int i = 0; i < nativeData.size(); ++i)
			{
				auto scores = VectorMarshaller::ToArray(nativeData[i].scores);
				auto loads = VectorMarshaller::ToArray(nativeData[i].loads);
				outputArray[i] = ComponentData(scores, loads);
			}
			return outputArray;
		}

		property GridParameters^ GridParams { GridParameters^ get() { return gridParams; } }
		property VoxelFeatureMatrix^ Matrix { VoxelFeatureMatrix^ get() { return matrix; } }

	private:
		GridParameters^ gridParams;
		VoxelFeatureMatrix^ matrix;
	};
}
