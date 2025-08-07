#pragma once
#include "PcaLibExport.h";
#include "VoxelFeatureMatrix.h"
#include "VoxelFeatureMatrixIon.h"
#include <memory>
#include <vector>
#include <span>

namespace Cameca::CustomAnalysis::PcaLib {
	class PCALIB_API VoxelFeatureMatrixBuilder
	{
	public:
		VoxelFeatureMatrixBuilder(const int voxelCount, const unsigned long long ionCount)
			: voxelCount(voxelCount), allMatrixIonData(ionCount), offset(0ull)
		{
		}

		void Update(std::span<VoxelFeatureMatrixIon> matrixIonData);
		VoxelFeatureMatrix Build();

	private:
		std::vector<VoxelFeatureMatrixIon> allMatrixIonData;
		const int voxelCount;
		unsigned long long offset;
	};
}

