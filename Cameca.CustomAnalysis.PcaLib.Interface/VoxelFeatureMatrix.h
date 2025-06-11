#pragma once
#include "ManagedWrapper.h"
#include "Cameca.CustomAnalysis.PcaLib/VoxelFeatureMatrix.h"
#include <memory>
#include "VectorMarshaller.h"

namespace Cameca::CustomAnalysis::PcaLib::Interface {
	public ref class VoxelFeatureMatrix : ManagedWrapper<PcaLib::VoxelFeatureMatrix>
	{
	public:
		VoxelFeatureMatrix(std::unique_ptr<PcaLib::VoxelFeatureMatrix> native)
		 : ManagedWrapper(native.release()) { }

		property int VoxelCount { int get() { return GetInstance()->GetVoxelCount(); } }
		property int FeatureCount { int get() { return GetInstance()->GetFeatureCount(); } }

		cli::array<int>^ GetVoxelIndices() {
			return VectorMarshaller::ToArray(GetInstance()->GetVoxelIndices());
		}
	};
}

