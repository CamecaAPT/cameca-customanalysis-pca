#pragma once

using namespace System::Text::Json::Serialization;

namespace Cameca::CustomAnalysis::PcaLib::Interface {
	public ref class GridParameters
	{
	public:
		[JsonConstructor]
		GridParameters(cli::array<double>^ gridStart, double voxelSize, cli::array<int>^ voxelCount)
			: gridStart(gridStart), voxelSize(voxelSize), voxelCount(voxelCount) { }

		property cli::array<double>^ GridStart { cli::array<double>^ get() { return gridStart; } }
		property double VoxelSize { double get() { return voxelSize; } }
		property cli::array<int>^ VoxelCount { cli::array<int>^ get() { return voxelCount; } }

	private:
		cli::array<double>^ gridStart;
		double voxelSize;
		cli::array<int>^ voxelCount;
	};
}

