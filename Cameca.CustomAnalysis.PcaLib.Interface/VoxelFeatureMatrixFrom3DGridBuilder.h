#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/VoxelFeatureMatrixFrom3DGridBuilder.h"
#include "VoxelFeatureMatrixIon.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {
	public ref class VoxelFeatureMatrixFrom3DGridBuilder : ManagedWrapper<PcaLib::VoxelFeatureMatrixFrom3DGridBuilder>
	{
	public:
		VoxelFeatureMatrixFrom3DGridBuilder(int voxelCount, int featureCount)
			: ManagedWrapper(new PcaLib::VoxelFeatureMatrixFrom3DGridBuilder(voxelCount, featureCount)) { }
		void Update(System::ReadOnlyMemory<float> dataForIon);
		VoxelFeatureMatrix^ Build();
	};
}

