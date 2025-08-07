#pragma once
using namespace System;
using namespace System::Runtime::InteropServices;

namespace Cameca::CustomAnalysis::PcaLib::Interface
{
	[StructLayout(LayoutKind::Sequential, Pack = 1)]
	public value struct VoxelFeatureMatrixIon
	{
	public:
		initonly int Voxel;
		initonly int Feature;

		VoxelFeatureMatrixIon(int voxel, int feature)
			: Voxel(voxel), Feature(feature) { }
	};
}