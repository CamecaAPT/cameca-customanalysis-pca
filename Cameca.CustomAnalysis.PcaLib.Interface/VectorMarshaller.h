#pragma once
#include <vector>

namespace Cameca::CustomAnalysis::PcaLib::Interface
{
	ref class VectorMarshaller abstract sealed
	{
	public:
		template<typename T>
		static cli::array<T>^ ToArray(const std::vector<T>& in)
		{
			std::size_t count = in.size();
			auto out = gcnew cli::array<T>(static_cast<int>(count));
			if (!in.empty())
			{
				const pin_ptr<T> pOut = &out[0];
				memcpy(pOut, in.data(), count * sizeof(T));
			}
			return out;
		}

		template<typename T>
		static std::vector<T> ToVector(System::Collections::Generic::IEnumerable<T>^ in)
		{
			std::vector<T> out;
			for each (float item in in)
			{
				out.push_back(item);
			}
			return out;
		}
	};
}