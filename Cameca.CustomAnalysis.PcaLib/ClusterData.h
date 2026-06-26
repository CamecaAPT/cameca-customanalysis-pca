#pragma once
#include <vector>

namespace Cameca::CustomAnalysis::PcaLib {
	struct ClusterData
	{
		float cost;
		std::vector<int> voxelIndex;
	};
}