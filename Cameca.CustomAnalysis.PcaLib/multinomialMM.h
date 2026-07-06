#ifndef __MULTINOMIAL_H__
#define __MULTINOMIAL_H__

// Copyright 2026 Michael R. Keenan
// Revised: 31-May-2026 08:59:42

#include "utilities.h"
#include <random>
#include <type_traits>
#include <Eigen/Dense>
#include <unsupported/Eigen/SpecialFunctions>

// Input arguments:
//     X: nFeatures x nSamples data matrix
//     nClust: scalar number of clusters to estimate
//     nReplicates: scalar number of times to repeat analysis, returning the "best"

// Outputs:
//     Centroid: nFeatures x nClust matrix of cluster centroids
//     idxClust: nSamples x 1 vector of zero-based cluster assignments
//     postP: nSamples x nClust matrix of posterior probabilities
//     fraction: cluster component proportions
//     cost: negative log likelihood of data given the model

using namespace Eigen;
template <typename T>
void initializeEM(Matrix<T, Dynamic, Dynamic>& mnProb, Vector<T, Dynamic>& fraction)
{
    std::uniform_real_distribution<T> dist(0, 1);
    fillRandom(mnProb, dist);
    // Normalize the multinomial feature-wise probabilities to sum to one    
    mnProb.array().rowwise() /= mnProb.array().colwise().sum();
    // Set all clusters to have equal size
    fraction.setConstant(1 / static_cast<T>(fraction.size()));
}

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void getGlobalVectors(const MatrixBase<derived>& X, Vector<T, Dynamic>& sumX, Vector<T, Dynamic>& gammaterm)
{
    auto ncol = X.cols();
    auto cols = X.colwise();
    sumX = cols.sum().transpose();
    gammaterm.array() = sumX.array() * sumX.array().log();
    for (auto i = 0; i < ncol; i++)
        gammaterm(i) -= (X.col(i).array() + 1).lgamma().sum();
}

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
T Estep(const MatrixBase<derived>& X, const Matrix<T, Dynamic, Dynamic>& mnProb,
    Vector<T, Dynamic>& fraction, const Vector<T, Dynamic>& sumX,
    const Vector<T, Dynamic>& gammaterm, Matrix<T, Dynamic, Dynamic>& postP)
{
    auto nrows = X.cols();
    auto ncols = fraction.size();

    // Compute the element-wise log-likelihood
    RowVector<T, Dynamic> onesrow = RowVector<T, Dynamic>::Ones(ncols);
    Vector<T, Dynamic> onescol = Vector<T, Dynamic>::Ones(nrows);
    RowVector<T, Dynamic> logf = log((fraction.transpose()).array());
    Matrix<T, Dynamic, Dynamic> logp_trans = log((mnProb.transpose()).array());
    RowVector<T, Dynamic> sump = mnProb.colwise().sum();
    Matrix<T, Dynamic, Dynamic> logLike(nrows, ncols);

    logLike = onescol * logf + gammaterm * onesrow - sumX * sump +
        (logp_trans * X).transpose();

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

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void Mstep(const MatrixBase<derived>& X, const Matrix<T, Dynamic, Dynamic>& postP,
    Matrix<T, Dynamic, Dynamic>& mnProb, Vector<T, Dynamic>& fraction, const Vector<T, Dynamic>& sumX)
{
    fraction = postP.colwise().sum().transpose();
    fraction /= fraction.template lpNorm<1>();
    Vector<T, Dynamic> normvec = (sumX.transpose() * postP).array().cwiseInverse();
    // normvec = normvec.array().cwiseInverse();
    mnProb = (X.derived() * postP) * normvec.asDiagonal();
}

template <typename derived, typename derivedV,
    typename T = typename MatrixBase<derived>::Scalar >
T trainModel(const MatrixBase<derived>& X, const int nClust, const int nReplicates,
    MatrixBase<derived>& Centroid, MatrixBase<derivedV>& indx,
    MatrixBase<derived>& postP, MatrixBase<derived>& fraction)
{
    constexpr int maxiter = 100;
    T tol;
    if constexpr (std::is_same<T, float>::value)
        tol = 1e-6;
    else
        tol = 1e-8;

    auto nFeatures = X.rows();
    auto nSamples = X.cols();
    Vector<T, Dynamic> sumX(nSamples);
    Vector<T, Dynamic> gammaterm(nSamples);

    Matrix<T, Dynamic, Dynamic> thisMNp(nFeatures, nClust);
    Matrix<T, Dynamic, Dynamic> thisPosteriorP(nSamples, nClust);
    Vector<T, Dynamic> thisfraction(nClust);

    getGlobalVectors(X, sumX, gammaterm);
    T bestnllk = std::numeric_limits<T>::infinity();
    for (auto rep = 0; rep < nReplicates; rep++) {
        initializeEM(thisMNp, thisfraction);

        T nllk;
        T oldnllk = std::numeric_limits<T>::infinity();
        for (auto i = 0; i < maxiter; i++) {
            nllk = Estep(X, thisMNp, thisfraction, sumX, gammaterm, thisPosteriorP);
            Mstep(X, thisPosteriorP, thisMNp, thisfraction, sumX);
            if (std::abs(oldnllk - nllk) <= tol * std::abs(nllk))
                break;
            else
                oldnllk = nllk;
        }
        if (nllk < bestnllk) {
            PermutationMatrix P = sortPermMat(thisfraction); // order by fraction
            bestnllk = nllk;
            Centroid = thisMNp * P;
            fraction = P.transpose() * thisfraction;
            postP = thisPosteriorP * P;
        }
    }

    // Centroid contains the multinomial probabilities, compute cluster centroids
    RowVector<T, Dynamic> Nk = nSamples * fraction.transpose(); // cluster responsibities
    RowVector<T, Dynamic> muN = (sumX.transpose() * postP).array() / Nk.array(); // mean number of cluster ions
    Centroid *= muN.asDiagonal(); // ion probabilities * number of ions

    // Make the cluster assignments
    rowmax2index(postP, indx);

    return bestnllk;
}

#endif /* __MULTINOMIAL_H__ */