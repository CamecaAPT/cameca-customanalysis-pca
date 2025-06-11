#pragma once
#include "PcaLibExport.h"
#include "VoxelFeatureMatrix.h"
#include <vector>
#include <optional>

namespace Cameca::CustomAnalysis::PcaLib {
	/// <summary>
	/// Wraps the result of a method that uses an iterative LAPACK algorithm that can
	/// potentially fail at runtime. Includes the function result info value for error display.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	template<typename T>
	struct PCALIB_API LapackResult
	{
		T value;
		int info;
	};

	struct ComponentData
	{
		std::vector<float> scores;
		std::vector<float> loads;
	};

	class PCALIB_API PrincipalComponentAnalysis
	{
	public:
		PrincipalComponentAnalysis(std::shared_ptr<const VoxelFeatureMatrixImpl> matrix)
			: matrix(std::move(matrix)) { }

		LapackResult<const std::vector<float>> GetEigenvalues();

		const int EstimateRankF(const int gaps, const int significance, const bool refine);

		std::vector<float> GetNoiseEigenvalues(const int rank);

		LapackResult<std::vector<ComponentData>> GetComponents(const int nComponents);

	private:
		/*
		 * Per Mike Keenan regarding adjustments to scores and loadings:
		 * The scores are O(10-3). The loadings in this case are O(1000).
		 * The data matrix is modeled as a product of the scores and loadings.
		 * So, if you multiply the scores by 1000 and divide the loadings by 1000,
		 * both factors will be O(1), which is more in line with the expected magnitude of data-matrix elements.
		 * This would probably be a good thing to do.
		 */
		const float scoresCoefficient = 1000.0f;
		const float loadingsCoefficient = 0.001f;
		std::shared_ptr<const VoxelFeatureMatrixImpl> matrix;
		std::optional<const LapackResult<const std::vector<float>>> eigenvalueResult;
	};
}
