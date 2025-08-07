#include "VoxelFeatureMatrixFrom3DGridBuilder.h"
#include <memory>

using namespace Cameca::CustomAnalysis::PcaLib::Interface;

VoxelFeatureMatrix^ VoxelFeatureMatrixFrom3DGridBuilder::Build() {
	return gcnew VoxelFeatureMatrix(std::make_unique<PcaLib::VoxelFeatureMatrix>(GetInstance()->Build()));
}
void VoxelFeatureMatrixFrom3DGridBuilder::Update(System::ReadOnlyMemory<float> dataForIon) {
	// create std::span from the memory
	auto handle = dataForIon.Pin();
	try
	{
		float* start = reinterpret_cast<float*>(handle.Pointer);
		std::span<float> span(start, dataForIon.Length);
		GetInstance()->Update(span);
	}
	finally {
		delete handle;
	}
}