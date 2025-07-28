#include "pch.h"
#include "VoxelFeatureMatrix.h"
#include "VoxelFeatureMatrixFrom3DGridBuilder.h"
#include <memory>
#include <span>
#include <tbb/parallel_for.h>
#include <tbb/enumerable_thread_specific.h>
#include <tbb/blocked_range.h>
#include <Eigen/Dense>
#include "VoxelFeatureMatrixImpl.h"

using namespace Cameca::CustomAnalysis::PcaLib;

void VoxelFeatureMatrixFrom3DGridBuilder::Update(std::span<float> dataForIon)
{
	std::copy(dataForIon.begin(), dataForIon.end(), allDataForIons.begin() + offset);
	offset += dataForIon.size();
}

VoxelFeatureMatrix VoxelFeatureMatrixFrom3DGridBuilder::Build() {
	using GridIndexType = int;

	GridIndexType compressedIndex = 0;
	std::vector<GridIndexType>voxelToIndex(voxelCount);
	std::vector<GridIndexType> indexToVoxel;


	// Build mapping for used voxels -- at least one feature channel has a non-zero value for that voxel
	for (unsigned long long voxIndex = 0; voxIndex < voxelCount; voxIndex++) {
		for (int featIndex = 0; featIndex < nFeatures; featIndex++) {
			float voxFeatValue = allDataForIons[voxIndex + (featIndex * voxelCount)];
			if (voxFeatValue != 0) {
				indexToVoxel.push_back(voxIndex);
				voxelToIndex[voxIndex] = compressedIndex++;
				break;
			}
		}
	}
	int nVoxels = compressedIndex;

	// Create matrix
	Eigen::MatrixXf X = Eigen::MatrixXf::Zero(nVoxels, nFeatures);
	for (unsigned long long voxIndex = 0; voxIndex < voxelCount; voxIndex++) {
		auto compressedIndex = voxelToIndex[voxIndex];
		for (int featIndex = 0; featIndex < nFeatures; featIndex++) {
			float voxFeatValue = allDataForIons[voxIndex + (featIndex * voxelCount)];
			X(compressedIndex, featIndex) = voxFeatValue;
		}
	}

	return VoxelFeatureMatrix(std::make_shared<VoxelFeatureMatrixImpl>(std::move(X), std::move(indexToVoxel)));
}
