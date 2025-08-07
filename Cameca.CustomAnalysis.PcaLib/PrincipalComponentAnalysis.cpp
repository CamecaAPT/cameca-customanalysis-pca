#include "pch.h"
#include "PrincipalComponentAnalysis.h"
#include <mkl.h>
#include <Eigen/Dense>
#include "VoxelFeatureMatrixImpl.h"
#include "RMT_rank.h"
#include <optional>

using namespace Eigen;
using namespace Cameca::CustomAnalysis::PcaLib;


void makePos(Map<MatrixXf>& T, Map<MatrixXf>& P)
{
    // Choose signs to make the loadings predominantly positive
    // There is probably a simpler way to do this

    VectorXf posX = P.cwiseMax(0).array().square().colwise().sum();
    VectorXf allX = P.array().square().colwise().sum();
    VectorXf signs = posX.array() / allX.array() - 0.5;
    signs = signs.array() / signs.array().abs();

    // Apply to both scores and loadings
    T = T * signs.asDiagonal();
    P = P * signs.asDiagonal();
}

void partialSVD(Map<MatrixXf>& T, Map<MatrixXf>& P)
{
    // Compute PCA of an arbitray factor model A*S'
    // Columns of T are orthogonal, columns of P are orthonormal
    // see Halko, et al, SIAM Review V53, No. 2, p.217-288, Sec 3.3.3

    int m = (int)T.rows();
    int p = (int)T.cols();
    int n = (int)P.rows();

    // Compute the thin QR factorization of T, overwriting T with QR representation
    float* tau = (float*)mkl_malloc(p * sizeof(float), 32);
    LAPACKE_sgeqrf(LAPACK_COL_MAJOR, m, p, T.data(), m, tau);

    // Extract R
    MatrixXf R = T.topRows(p).triangularView<Upper>();  // the upper triangle is R

    // Generate Q matrix, overwriting T
    LAPACKE_sorgqr(LAPACK_COL_MAJOR, m, p, p, T.data(), m, tau);

    // Compute the svd of P*R', overwrite P with left singular vectors
    P *= R.transpose();
    MatrixXf rsv(p, p);  // right singular vectors
    VectorXf sval(p);   // singular values
    LAPACKE_sgesdd(LAPACK_COL_MAJOR, 'O', n, p, P.data(), n, sval.data(), NULL, n, rsv.data(), p);
    rsv.transposeInPlace();  // we need rsv, LAPACK returns its transpose

    // Scale Q to produce output T
    T *= rsv * sval.asDiagonal();

    mkl_free(tau);
}

void varimax(Map<MatrixXf>& T, Map<MatrixXf>& P)
{
    // Varimax rotation using the Lawley & Maxwell algorithm (p. 72).
    // Assumes columns of T are orthonormal
    int m = (int)T.rows();
    int p = (int)T.cols();
    int n = (int)P.rows();

    // Copy T to T0, overwrite T with varimax iterations
    MatrixXf T0 = T;

    int maxiter = 500;
    const float tol = 1.0e-8F;
    float cost, oldCost = 0.0;

    MatrixXf C(m, p);
    MatrixXf B(p, p);
    MatrixXf R(p, p);
    MatrixXf lsv(p, p);
    MatrixXf rsvt(p, p);
    VectorXf sval(p);

    for (int i = 0; i < maxiter; i++) {
        C = T.array().cube() - T.array().rowwise() * T.array().square().colwise().mean();
        B = T0.transpose() * C;
        LAPACKE_sgesdd(LAPACK_COL_MAJOR, 'A', p, p, B.data(), p, sval.data(), lsv.data(), p, rsvt.data(), p);
        R = lsv * rsvt;
        T = T0 * R;
        cost = sval.sum();
        if ((cost - oldCost) / cost < tol)
            break;
        oldCost = cost;
    }

    // Apply rotation to P
    P *= R;
}

// This is an exported function.
int doPCA(
    const MatrixXf& X,
    const int nComponents,
    float* scores,
    float* loads,
    const float scoresCoefficient,
    const float loadsCoefficient)
{
    const int nVoxels = X.rows();
    const int nFeatures = X.cols();

    lapack_int n, il = 0, iu = 0, itype = 1, ZERO = 0, info = 0;
    char range, All = 'A', Some = 'I', jobz = 'N', uplo = 'U', trans = 'T';
    float alpha = 1.0, beta = 0.0;

    float abstol = -1; // 2*dlamch("S");

    // Map outputs to Eigen matrices
    Map<MatrixXf> T(scores, nVoxels, nComponents);
    Map<MatrixXf> P(loads, nFeatures, nComponents);

    // Compute the mean spectrum
    VectorXf meanX = X.colwise().mean();

    // Allocate memory for the data covariance matrix
    MatrixXf CXP = MatrixXf::Zero(nFeatures, nFeatures);

    // Compute the data covariance matrix using the BLAS and make a copy
    ssyrk(&uplo, &trans, &nFeatures, &nVoxels, &alpha, X.data(), &nVoxels,
        &beta, CXP.data(), &nFeatures);
    CXP = CXP / (float)nVoxels;
    MatrixXf CXPcopy = CXP;

    // Construct the noise covariance matrix assuming Poisson statistics
    MatrixXf CV = meanX.asDiagonal();

    // Compute all eigenvalues with LAPACK
    // Note: the CXP and CV matrix will be destroyed
    n = nFeatures;

    // Create matrices for eigenvectors and truncated eigenvalues
    MatrixXf V = MatrixXf::Zero(nFeatures, nComponents);
    VectorXf Etrunc(nComponents);

    int nevals = nComponents;
    range = Some;
    jobz = 'V';
    iu = n;
    il = iu - nevals + 1;
    int* ifail = (int*)mkl_malloc(nFeatures * sizeof(int), 32);
    info = LAPACKE_ssygvx(LAPACK_COL_MAJOR, itype, jobz, range, uplo, n, CXP.data(), n, CV.data(), n,
        NULL, NULL, il, iu, abstol, &nevals, Etrunc.data(), V.data(), nFeatures, ifail);
    //if (info != ZERO)

    // eigenvectors are sorted in ascending order -- make descending
    V.rowwise().reverseInPlace();

    // compute scores T = X * V;
    //trans = 'N';
    //dgemm(&trans, &trans, &nVoxels, &nComponents, &nFeatures, &alpha, X.data(), &nVoxels, V.data(), &nFeatures, &beta, T.data(), &nVoxels);
    cblas_sgemm(CblasColMajor, CblasNoTrans, CblasNoTrans, nVoxels, nComponents, nFeatures, alpha, X.data(), nVoxels, V.data(), nFeatures, beta, T.data(), nVoxels);

    // compute loadings P = CV * V;  recall CV = diag(meanX) so this is equivalent to row-wise scaling with the mean
    P = V.array().colwise() * meanX.array();

    // Orthogonalize factor model such that T has orthonormal columns
    partialSVD(P, T);

    // Perform spatial varimax rotation
    varimax(T, P);

    // Make loadings predominantly positive
    makePos(T, P);

    mkl_free(ifail);

    T *= scoresCoefficient;
    P *= loadsCoefficient;

    return info;
}


LapackResult<const std::vector<float>> PrincipalComponentAnalysis::GetEigenvalues()
{
    if (eigenvalueResult.has_value())
    {
        return eigenvalueResult.value();
    }

    auto nevals = matrix->GetFeatureCount();
    auto nFeatures = matrix->GetFeatureCount();
    auto nVoxels = matrix->GetVoxelCount();

    lapack_int n, il = 0, iu = 0, itype = 1, ZERO = 0, info = 0;
    char range, All = 'A', Some = 'I', jobz = 'N', uplo = 'U', trans = 'T';
    float alpha = 1.0, beta = 0.0;

    float abstol = -1; // 2*dlamch("S");

    VectorXf E = VectorXf::Zero(nevals);

    // Construct the data matrix
    const MatrixXf& X = matrix->GetMatrix();

    // Compute the mean spectrum
    VectorXf meanX = X.colwise().mean();

    // Allocate memory for the data covariance matrix
    MatrixXf CXP = MatrixXf::Zero(nFeatures, nFeatures);

    // Compute the data covariance matrix using the BLAS and make a copy
    ssyrk(&uplo, &trans, &nFeatures, &nVoxels, &alpha, X.data(), &nVoxels,
        &beta, CXP.data(), &nFeatures);
    CXP = CXP / (float)nVoxels;
    MatrixXf CXPcopy = CXP;

    // Construct the noise covariance matrix assuming Poisson statistics
    MatrixXf CV = meanX.asDiagonal();

    // Compute all eigenvalues with LAPACK
    // Note: the CXP and CV matrix will be destroyed
    n = nFeatures;
    nevals = nFeatures;
    range = All;
    jobz = 'N';
    info = LAPACKE_ssygvx(LAPACK_COL_MAJOR, itype, jobz, range, uplo, n, CXP.data(), n, CV.data(), n,
        NULL, NULL, il, iu, abstol, &nevals, E.data(), NULL, nevals, NULL);
    //if (info != ZERO)  The algorithm didn't complete properly, need error checking code

    // eigenvalues are sorted in ascending order -- make descending
    E.reverseInPlace();

    const std::vector<float> result(E.data(), E.data() + E.size());
    eigenvalueResult.emplace(std::move(LapackResult<const std::vector<float>>(result, info)));
    return eigenvalueResult.value();
}

const int PrincipalComponentAnalysis::EstimateRankF(const int gaps, const int significance, const bool refine)
{
    if (!eigenvalueResult.has_value())
    {
        PrincipalComponentAnalysis::GetEigenvalues();
    }
    // Check for error state
    if (eigenvalueResult.value().info != 0)
    {
        return 0;
    }
    // Copy due to issues getting const reference working
    auto evals = eigenvalueResult.value().value;

    const auto nObs = matrix->GetVoxelCount();
    Map<VectorXf> mEvals(evals.data(), evals.size());
    return EstimateRank(mEvals, nObs, gaps, significance, refine);
}

std::vector<float> PrincipalComponentAnalysis::GetNoiseEigenvalues(int rank)
{
    if (!eigenvalueResult.has_value())
    {
        PrincipalComponentAnalysis::GetEigenvalues();
    }
    // Check for error state
    if (eigenvalueResult.value().info != 0)
    {
        return std::vector<float>();
    }

    const auto nObs = matrix->GetVoxelCount();
    const auto nEvals = eigenvalueResult.value().value.size();

    std::vector<float> data(nEvals - rank, 0.0f);
    Map<VectorXf> y(data.data(), data.size());
    MarchenkoPasturDist<float> MPdist(nObs, nEvals);
    MPdist.NoiseEvals(rank, y);
    return data;
}

LapackResult<std::vector<ComponentData>> PrincipalComponentAnalysis::GetComponents(const int nComponents)
{
    const int nVoxels = matrix->GetVoxelCount();
    const int nFeatures = matrix->GetFeatureCount();
    const float* data = matrix->GetMatrix().data();

    std::vector<float> scores(nVoxels * nComponents, 0.0f);
    std::vector<float> loads(nFeatures * nComponents, 0.0f);

    auto info = doPCA(
        matrix->GetMatrix(),
        nComponents,
        scores.data(),
        loads.data(),
        scoresCoefficient,
        loadingsCoefficient);

    std::vector<ComponentData>componentData;
    componentData.reserve(nComponents);
    for (auto i = 0; i < nComponents; ++i)
    {
        std::vector<float> compScores(scores.begin() + (nVoxels * i), scores.begin() + (nVoxels * (i + 1)));
        std::vector<float> compLoads(loads.begin() + (nFeatures * i), loads.begin() + (nFeatures * (i + 1)));

        componentData.push_back(ComponentData{ compScores, compLoads });
    }

    return LapackResult<std::vector<ComponentData>>{ componentData, info };
}

