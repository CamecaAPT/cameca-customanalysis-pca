#pragma once
using namespace System;
using namespace System::Runtime::InteropServices;

namespace Cameca::CustomAnalysis::PcaLib::Interface
{
	[StructLayout(LayoutKind::Sequential, Pack = 1)]
	public value struct ClusterData
	{
	public:
		float Cost;
		initonly cli::array<int>^ VoxelIndex;

		ClusterData(float cost, cli::array<int>^ voxelIndex)
			: Cost(cost), VoxelIndex(voxelIndex) {
		}
	};
}