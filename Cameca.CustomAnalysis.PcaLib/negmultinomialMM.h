#ifndef __NEGMULTINOMIAL_H__
#define __NEGMULTINOMIAL_H__

// Copyright 2026 Michael R. Keenan
// Revised: 31-May-2026 08:59:42

#include "utilities.h"
#include <random>
#include <cmath>
#include <type_traits>
#include <Eigen/Dense>
#include <unsupported/Eigen/SpecialFunctions>

// Input arguments:
//     X: nFeatures x nSamples data matrix
//     nComp: scalar number of clusters to estimate
//     nReplicates: scalar number of times to repeat analysis, returning the "best"

// Outputs:
//     Centroid: nFeatures x nComp matrix of cluster centroids
//     idxClust: nSamples x 1 vector of zero-based cluster assignments
//     postP: nSamples x nComp matrix of posterior probabilities
//     fraction: cluster component proportions
//     cost: negative log likelihood of data given the model
//     NMr: vector of negative multinomial overdispersion parameters
//     NMp: matrix of negative multinomial probability parameters

// see: Hua ZHOU and Kenneth LANGE
// MM algorithms for some discrete multivariate distributions
// Journal of Computational and Graphical Statistics, 19, Pages 645–665

using namespace Eigen;

// Vectors that depend only on the constant data matrix
template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void getGlobalVectors(const MatrixBase<derived>& X,
    Vector<T, Dynamic>& sumX, VectorXi& sumXasIndex,
    Vector<T, Dynamic>& factorialSum)
{
    auto ncol = X.cols();
    auto cols = X.colwise();
    sumX = cols.sum().transpose();
    sumXasIndex = sumX.template cast<int>();
    sumXasIndex.array() -= 1;
    // factorialSum.array() =sumX.array() * sumX.array().log();
    for (auto i = 0; i < ncol; i++)
        factorialSum(i) = (X.col(i).array() + 1).lgamma().sum();
}

// Random initialization for iterative EM algorithm
// Algorithm is more stable if posterior probabilities are initialized and
// M-step is run first to update the model parameters
template <typename T>
void initializeEM(const Vector<T, Dynamic>& sumX, Matrix<T, Dynamic, Dynamic>& nmProb,
    Vector<T, Dynamic>& B, Matrix<T, Dynamic, Dynamic>& postP, Vector<T, Dynamic>& fraction)
{
    std::uniform_real_distribution<T> dist(0, 1);
    fillRandom(nmProb, dist);
    // Normalize the multinomial feature-wise probabilities to sum to one    
    nmProb.array().rowwise() /= nmProb.array().colwise().sum();
    // Generate and normalize random posterior probabilities
    fillRandom(postP, dist);
    postP.array().colwise() /= postP.array().rowwise().sum();

    // Estimate global negative binomial R parameter using method of moments
    T meanX = sumX.mean();
    T varX = (sumX.array() - meanX).square().sum() / sumX.size();
    T stdX = sqrt(varX);
    T R = (meanX * meanX) / (varX - meanX);
    // randomly choose elements of B that are near and on either side of R
    std::uniform_real_distribution<T> distB(-stdX, stdX);
    do {
        fillRandom(B, distB);
    } while ((B.array() < 0).all() || (B.array() > 0).all());
    B.array() += R;

    // Set all clusters to have equal size
    fraction.setConstant(1 / static_cast<T>(fraction.size()));
}

// E-step *****************************************************************
// Compute the posterior probabilities of the data given the model
template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
T Estep(const MatrixBase<derived>& X, const Matrix<T, Dynamic, Dynamic>& nmProb,
    Vector<T, Dynamic>& nmB, Vector<T, Dynamic>& fraction, const Vector<T, Dynamic>& sumX,
    const Vector<T, Dynamic>& factorialSum, Matrix<T, Dynamic, Dynamic>& postP)
{
    auto nSamples = X.cols();
    auto nFeatures = X.rows();
    auto nClust = fraction.size();

    Matrix<T, Dynamic, Dynamic> p = nmProb.topRows(nFeatures);
    RowVector<T, Dynamic> p0 = nmProb.bottomRows(1);

    // Compute the column-wise log-likelihood
    Matrix<T, Dynamic, Dynamic> logLike(nSamples, nClust);

    for (auto i = 0; i < nClust; i++) {
        T constterm = std::log(fraction[i]) + nmB[i] * std::log(p0[i]) - std::lgamma(nmB[i]);
        logLike.col(i) = (log(p.col(i).array()).matrix().transpose() * X).transpose() - factorialSum;
        logLike.col(i).array() += (sumX.array() + nmB[i]).lgamma() + constterm;
    }

    // limit the range of the log-likelihood since we will exponentiate
    T limit;
    if constexpr (std::is_same<T, float>::value)
        limit = 85;
    else
        limit = 700;
    boundMatrix(logLike, -limit, limit);

    // compute the log-likelihoods and posterior probabilities
    T negLogLike = logsumexp(logLike, postP);

    return negLogLike;
}

// M-step ******************************************************************
// Implement the sample-wise block relaxation update, see ref eq 3.14 ff
// The number of unique elements equals the global maximum total ion count
template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void blockUpdate(const T B, const T denom, MatrixBase<derived>& blockVec)
{
    auto nElem = blockVec.size();
    blockVec.setLinSpaced(nElem, 0.0, static_cast<T>(nElem - 1));
    blockVec[0] = -(B / (B + blockVec[0])) / denom;
    for (auto i = 1; i < nElem; i++)
        blockVec[i] = blockVec[i - 1] - (B / (B + blockVec[i])) / denom;
}

// Fill voxel-wise update parameter by gathering from blockUpdate
template <typename derivedR, typename derivedI, typename T = typename MatrixBase<derivedR>::Scalar >
void gatherVector(const MatrixBase<derivedR>& data, const MatrixBase<derivedI>& index,
    MatrixBase<derivedR>& result)
{
    // Map data elements into the result vector based on indices
    std::transform(index.begin(), index.end(), result.begin(),
        [&data](int index) { return data[index]; });
}

// Reestimate parameters
template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void Mstep(const MatrixBase<derived>& X, const Matrix<T, Dynamic, Dynamic>& postP,
    Matrix<T, Dynamic, Dynamic>& nmProb, Vector<T, Dynamic>& nmB,
    Vector<T, Dynamic>& fraction, const VectorXi& sumXasIndex)
{
    // estimate component proportions
    Vector<T, Dynamic> sumPostP = postP.colwise().sum().transpose();
    fraction = sumPostP / sumPostP.sum();
    auto nClust = fraction.size();
    auto nFeatures = X.rows(); // also equal to the zero-base index of p0
    auto nSamples = X.cols();

    // Use block relaxation approach to update model parameters B and p
    Matrix<T, Dynamic, Dynamic> wtX = (X * postP);
    RowVector<T, Dynamic> sumwtX = postP.colwise().sum();
    Vector<T, Dynamic> updaterVec(sumXasIndex.maxCoeff() + 1);
    Vector<T, Dynamic> rterm(nSamples);
    RowVector<T, Dynamic> p0(nClust);
    Vector<T, Dynamic> old_B(nClust);

    constexpr int maxiterInnerUpdate = 100;
    constexpr T tol = 1e-4;
    for (auto i = 0; i < nClust; i++) {
        for (auto j = 0; j < maxiterInnerUpdate; j++) {
            old_B[i] = nmB[i];
            p0 = nmProb.col(i);
            T constterm = sumPostP[i] * std::log(p0[nFeatures]);
            blockUpdate(nmB[i], constterm, updaterVec);
            gatherVector(updaterVec, sumXasIndex, rterm);
            nmB[i] = postP.col(i).transpose() * rterm;
            p0.head(nFeatures) = wtX.col(i);
            p0[nFeatures] = sumwtX[i] * nmB[i];
            nmProb.col(i) = p0 / p0.sum();
            if (std::abs(old_B[i] - nmB[i]) <= tol * std::abs(nmB[i]))
                break;
            else
                old_B[i] = nmB[i];
        }
    }
}

template <typename derived, typename derivedI,
    typename T = typename MatrixBase<derived>::Scalar >
T trainModel(const MatrixBase<derived>& X, const int nComp, const int nReplicates,
    MatrixBase<derived>& Centroid, MatrixBase<derivedI>& clustID,
    MatrixBase<derived>& postP, MatrixBase<derived>& fraction,
    MatrixBase<derived>& NMr, MatrixBase<derived>& NMp)
{
    constexpr int maxiter = 200;
    T tol;
    if constexpr (std::is_same<T, float>::value)
        tol = 1e-6;
    else
        tol = 1e-8;

    auto nFeatures = X.rows();
    auto nSamples = X.cols();
    auto nClust = fraction.size();

    // global constant vectors derived from the data matrix
    Vector<T, Dynamic> sumX(nSamples);
    VectorXi sumXasIndex(nSamples);
    Vector<T, Dynamic> factorialSum(nSamples);
    getGlobalVectors(X, sumX, sumXasIndex, factorialSum);

    // Repeat the analysis nReplicates times keeping only the best result
    // local matrices/vectors of model parameters overwritten each replicate
    Matrix<T, Dynamic, Dynamic> thisNMp(nFeatures + 1, nClust);
    Matrix<T, Dynamic, Dynamic> thisPosteriorP(nSamples, nClust);
    Vector<T, Dynamic> thisB(nClust);
    Vector<T, Dynamic> thisfraction(nClust);

    Matrix<T, Dynamic, Dynamic> I(nClust, nClust); I.setIdentity();

    T bestnllk = std::numeric_limits<T>::infinity();
    for (auto rep = 0; rep < nReplicates; rep++) {
        initializeEM(sumX, thisNMp, thisB, thisPosteriorP, thisfraction);

        T nllk;
        T oldnllk = std::numeric_limits<T>::infinity();
        for (auto i = 0; i < maxiter; i++) {
            Mstep(X, thisPosteriorP, thisNMp, thisB, thisfraction, sumXasIndex);
            nllk = Estep(X, thisNMp, thisB, thisfraction, sumX, factorialSum, thisPosteriorP);
            if (std::abs(oldnllk - nllk) <= tol * std::abs(nllk))
                break;
            else
                oldnllk = nllk;
        }
        if (nllk < bestnllk) {  // best so far, sort by fraction and assign model to output
            PermutationMatrix P = sortPermMat(thisfraction);
            bestnllk = nllk;
            NMp = thisNMp * P;
            NMr = P.transpose() * thisB;
            fraction = P.transpose() * thisfraction;
            postP = thisPosteriorP * P;
        }
    }

    // Compute cluster centroids
    Vector<T, Dynamic> scaleFactor = NMr.array() / NMp.bottomRows(1).transpose().array();
    Centroid = NMp.topRows(nFeatures) * scaleFactor.asDiagonal();

    // Make the cluster assignments
    rowmax2index(postP, clustID);

    return bestnllk;
}

#endif /* __NEGMULTINOMIAL_H__ */