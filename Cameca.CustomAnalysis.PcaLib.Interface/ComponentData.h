#pragma once
using namespace System;
using namespace System::Runtime::InteropServices;

namespace Cameca::CustomAnalysis::PcaLib::Interface
{
	[StructLayout(LayoutKind::Sequential, Pack = 1)]
	public value struct ComponentData
	{
	public:
		initonly cli::array<float>^ Scores;
		initonly cli::array<float>^ Loads;

		ComponentData(cli::array<float>^ scores, cli::array<float>^ loads)
			: Scores(scores), Loads(loads) { }
	};
}