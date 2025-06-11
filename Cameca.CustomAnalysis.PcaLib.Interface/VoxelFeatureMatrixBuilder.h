#pragma once
#include "ManagedWrapper.h"
#include "VoxelFeatureMatrix.h"
#include "Cameca.CustomAnalysis.PcaLib/VoxelFeatureMatrixBuilder.h"
#include "VoxelFeatureMatrixIon.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {
	public ref class VoxelFeatureMatrixBuilder : ManagedWrapper<PcaLib::VoxelFeatureMatrixBuilder>
	{
	public:
		VoxelFeatureMatrixBuilder(int voxelCount, unsigned long long ionCount)
			: ManagedWrapper(new PcaLib::VoxelFeatureMatrixBuilder(voxelCount, ionCount)) { }
		void Update(System::ReadOnlyMemory<VoxelFeatureMatrixIon> matrixIonData);
		VoxelFeatureMatrix^ Build();
	};
}

