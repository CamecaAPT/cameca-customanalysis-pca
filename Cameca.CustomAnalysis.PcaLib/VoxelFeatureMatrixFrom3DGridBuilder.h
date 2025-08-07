#pragma once
#include "PcaLibExport.h";
#include "VoxelFeatureMatrix.h"
#include "VoxelFeatureMatrixIon.h"
#include <memory>
#include <vector>
#include <span>

namespace Cameca::CustomAnalysis::PcaLib {
	class PCALIB_API VoxelFeatureMatrixFrom3DGridBuilder
	{
	public:
		VoxelFeatureMatrixFrom3DGridBuilder(const int voxelCount, const int nFeatures)
			: voxelCount(voxelCount), allDataForIons(voxelCount * nFeatures), offset(0ull), nFeatures(nFeatures)
		{
		}

		void Update(std::span<float> dataForIon);
		VoxelFeatureMatrix Build();
	private:
		std::vector<float> allDataForIons;
		const int voxelCount;
		unsigned long long offset;
		int nFeatures;
	};
}
