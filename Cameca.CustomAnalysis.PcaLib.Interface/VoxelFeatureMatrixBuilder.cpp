#include "VoxelFeatureMatrixBuilder.h"
#include "Cameca.CustomAnalysis.PcaLib/VoxelFeatureMatrixIon.h"
#include <memory>

using namespace Cameca::CustomAnalysis::PcaLib::Interface;

VoxelFeatureMatrix^ VoxelFeatureMatrixBuilder::Build() {
	return gcnew VoxelFeatureMatrix(std::make_unique<PcaLib::VoxelFeatureMatrix>(GetInstance()->Build()));
}
void VoxelFeatureMatrixBuilder::Update(System::ReadOnlyMemory<VoxelFeatureMatrixIon> matrixIonData) {
	// create std::span from the memory
	auto handle = matrixIonData.Pin();
	try
	{
		Cameca::CustomAnalysis::PcaLib::VoxelFeatureMatrixIon* start = reinterpret_cast<Cameca::CustomAnalysis::PcaLib::VoxelFeatureMatrixIon*>(handle.Pointer);
		std::span<Cameca::CustomAnalysis::PcaLib::VoxelFeatureMatrixIon> span(start, matrixIonData.Length);
		GetInstance()->Update(span);
	}
	finally {
		delete handle;
	}
}