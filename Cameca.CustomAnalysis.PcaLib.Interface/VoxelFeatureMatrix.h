#pragma once
#include "ManagedWrapper.h"
#include "Cameca.CustomAnalysis.PcaLib/VoxelFeatureMatrix.h"
#include <memory>
#include "VectorMarshaller.h"

using namespace System::Runtime::InteropServices;

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

        // Data to map to ReadOnlyMemory in .NET without copying via a custom MemoryManager
		property System::IntPtr DataPointer {
			System::IntPtr get() {
				const float* ptr = GetInstance()->GetData();
				return System::IntPtr(const_cast<float*>(ptr));
			}
		}
		property int DataLength{ int get() { return GetInstance()->GetDataLength(); } }

		static VoxelFeatureMatrix^ FromData(
			System::ReadOnlySpan<float> matrixData,
			int rows,
			int cols,
			System::ReadOnlySpan<int> voxelIndicesData)
		{
			pin_ptr<const float> pMatrix = &MemoryMarshal::GetReference(matrixData);
			pin_ptr<const int> pIndices = &MemoryMarshal::GetReference(voxelIndicesData);

			// Call our native restoration logic
			auto nativeMatrix = PcaLib::VoxelFeatureMatrix::FromData(
				pMatrix,
				rows,
				cols,
				pIndices,
				voxelIndicesData.Length
			);

			// Package it back up into our managed tracking wrapper
			auto nativeUniquePtr = std::make_unique<PcaLib::VoxelFeatureMatrix>(std::move(nativeMatrix));
			return gcnew VoxelFeatureMatrix(std::move(nativeUniquePtr));
		}
	};
}

