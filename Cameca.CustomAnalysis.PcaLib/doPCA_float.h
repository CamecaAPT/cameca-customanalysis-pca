// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the DOPCAFLOAT_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// DOPCAFLOAT_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef DOPCAFLOAT_EXPORTS
#define DOPCAFLOAT_API __declspec(dllexport)
#else
#define DOPCAFLOAT_API __declspec(dllimport)
#endif

extern "C" DOPCAFLOAT_API void doEigen(const int nVoxels, const int nFeatures,
	const float* data,
	int nevals, float* evals);

extern "C" DOPCAFLOAT_API void doPCA(const int nVoxels, const int nFeatures,
	const float* data,
	const int nIons, const int nComponents, int nevals,
	float* scores, float* loads, float* evals);

