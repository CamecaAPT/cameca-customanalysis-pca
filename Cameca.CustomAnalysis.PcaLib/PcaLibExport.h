#pragma once
#ifdef PCALIB_EXPORTS
#define PCALIB_API __declspec(dllexport)
#else
#define PCALIB_API __declspec(dllimport)
#endif
