#pragma once
#ifndef __ONMF_H__
#define __ONMF_H__

#include "utilities.h"
#include <cstdio>
#include <optional>
#include <limits>
#include <numeric>
#include <Eigen/Dense>

using namespace Eigen;

template <typename T> using MatrixT = Matrix<T, Dynamic, Dynamic>;
template <typename T> using VectorT = Matrix<T, Dynamic, 1>;

// Compute the log of the multivariate gaussian pdf at x given mu & Sigma
// Samples are columns of X, result is an nSample-vector
template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void logmvnpdf(const MatrixBase<derived>& X, const VectorT<T>& mu,
    const MatrixT<T>& Sigma, VectorT<T>& y)
{
    constexpr double log2pi = 1.837877066409345;

    int nVars = X.rows();
    int nSamples = X.cols();

    // compute the cholesky decomposition of Sigma, lower triangle
    LLT<MatrixT<T>> lltOfC(Sigma);
    MatrixT<T> L = lltOfC.matrixL();
    MatrixT<T> invL = L.inverse();

    // compute -log(det(Sigma))/2 from the cholesky factor
    VectorT<T> diagL = L.diagonal();
    T detTerm = std::accumulate(diagL.begin(), diagL.end(), 1.0, std::multiplies<T>());
    detTerm = -log(detTerm);

    const T constTerm = -(T)nVars * log2pi / 2.0 + detTerm;

    // Compute mahalanobis term and log pdf
    VectorT<T> diffvec(nVars);
    for (auto i = 0; i < nSamples; i++) {
        diffvec = X.col(i).array() - mu.array();
        y(i) = -0.5 * (invL * diffvec).squaredNorm() + constTerm;
    }
}

// Vectors that depend only on the constant data matrix
template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void getGlobalVectors(const MatrixBase<derived>& X, const bool pcaModel,
    VectorT<T>& rowmeanX, VectorT<T>& colsumX)
{
    // colsumX is a vector of length nSamples, rowmeanX is a vector of length nFeatures
    if (pcaModel) {
        colsumX = X.rowwise().sum();
        rowmeanX = X.colwise().mean().transpose();
    }
    else {
        colsumX = X.colwise().sum().transpose();
        rowmeanX = X.rowwise().mean();
    }
}

template < typename T>
void initializeEM(const VectorT<T> meanX, const T regParam,
    MatrixT<T>& postP, VectorT<T>& fraction,
    MatrixT<T>& mu, MatrixT<T>& Sigma)
{
    int nClust = fraction.size();
    int nVars = meanX.size();

    std::uniform_real_distribution<T> dist(0, 1);
    fillRandom(mu, dist);

    // Generate and normalize random posterior probabilities
    fillRandom(postP, dist);
    postP.array().colwise() /= postP.array().rowwise().sum();

    // Set all clusters to have equal size
    fraction.setConstant(1 / static_cast<T>(nClust));

    // set Sigma to be the identity
    VectorT<T> regMeanX = VectorT<T>::Ones(meanX.size());
    for (auto i = 0; i < nClust; i++) {
        Sigma.block(0, i * nVars, nVars, nVars) = regMeanX.asDiagonal();
    }
}

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
T Estep(const MatrixBase<derived>& X, const bool pcaModel, const MatrixT<T>& mu, const MatrixT<T>& Sigma,
    VectorT<T>& fraction, MatrixT<T>& postP)
{
    auto nClust = fraction.size();
    auto nSamples = pcaModel ? X.rows() : X.cols();
    auto nFeatures = pcaModel ? X.cols() : X.rows();

    MatrixT<T> logLike(nSamples, nClust);
    for (auto i = 0; i < nClust; i++) {
        T constterm = std::log(fraction[i]);
        MatrixT<T> clustSigma = Sigma.block(0, i * nFeatures, nFeatures, nFeatures);
        VectorT<T> clustmu = mu.col(i);
        VectorT<T> clustLL = logLike.col(i);
        if (pcaModel) {
            MatrixT<T> scoresT = X.transpose();
            logmvnpdf(scoresT, clustmu, clustSigma, clustLL);
        }
        else {
            logmvnpdf(X, clustmu, clustSigma, clustLL);
        }
        logLike.col(i) = clustLL;
        logLike.col(i).array() += constterm;
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

template <typename derived, typename T = typename MatrixBase<derived>::Scalar >
void Mstep(const MatrixBase<derived>& X, const bool pcaModel, const MatrixT<T>& postP, const VectorT<T>& regVec,
    MatrixT<T>& mu, MatrixT<T>& Sigma, VectorT<T>& fraction)
{
    // estimate fractions
    VectorT<T> sumPostP = postP.colwise().sum().transpose();
    fraction = sumPostP / sumPostP.sum();
    auto nClust = fraction.size();
    // auto nSamples = pcaModel ? X.rows() : X.cols();
    auto nFeatures = pcaModel ? X.cols() : X.rows();

    // estimate mu
    RowVector<T, Dynamic> Nk = postP.colwise().sum();
    if (pcaModel)
        mu = X.transpose() * postP;
    else
        mu = X * postP;

    mu.array().rowwise() /= Nk.array();

    // estimate Sigma
    MatrixT<T> clustSigma(nFeatures, nFeatures);
    MatrixT<T> crossTerm(nFeatures, nFeatures);
    for (auto i = 0; i < nClust; i++) {
        if (pcaModel)
            crossTerm = (X.transpose() * postP.col(i)) * mu.col(i).transpose();
        else
            crossTerm = (X * postP.col(i)) * mu.col(i).transpose();
        crossTerm += crossTerm.transpose().eval();
        if (pcaModel)
            clustSigma = X.transpose() * postP.col(i).asDiagonal() * X +
            Nk[i] * (mu.col(i) * mu.col(i).transpose()) - crossTerm;
        else
            clustSigma = X * postP.col(i).asDiagonal() * X.transpose() +
            Nk[i] * (mu.col(i) * mu.col(i).transpose()) - crossTerm;
        clustSigma.array() /= Nk[i];
        clustSigma += regVec.asDiagonal();
        Sigma.block(0, i * nFeatures, nFeatures, nFeatures) = clustSigma;
    }

}

template <typename derived, typename derivedI,
    typename T = typename MatrixBase<derived>::Scalar >
T trainModel(const MatrixBase<derived>& X, const std::optional<Matrix<T, Dynamic, Dynamic>> loadings,
    const int nClust, const int nReplicates, const T regParam,
    MatrixBase<derived>& Centroid, MatrixBase<derived>& postP,
    MatrixBase<derivedI>& clustID, MatrixBase<derived>& mu, MatrixBase<derived>& Sigma)
{
    constexpr int maxiter = 100;
    T tol;
    if constexpr (std::is_same<T, float>::value)
        tol = 1e-5;
    else
        tol = 1e-6;
    auto nFeatures = loadings.has_value() ? X.cols() : X.rows();
    auto nSamples = loadings.has_value() ? X.rows() : X.cols();

    VectorT<T> rowmeanX(nFeatures);
    VectorT<T> colsumX(nSamples);
    VectorT<T> regVec = VectorT<T>::Constant(nFeatures, regParam);
    getGlobalVectors(X, loadings.has_value(), rowmeanX, colsumX);

    Centroid.setZero();
    clustID.setZero();

    VectorT<T> thisfraction(nClust);
    MatrixT<T> thismu(nFeatures, nClust);
    MatrixT<T> thisSigma(nFeatures, nClust * nFeatures);
    MatrixT<T> thisPostP(nSamples, nClust);

    T thiscost;
    T oldcost = std::numeric_limits<T>::infinity();
    T cost = oldcost;
    for (auto rep = 0; rep < nReplicates; rep++) {
        initializeEM(rowmeanX, regParam, thisPostP, thisfraction, thismu, thisSigma);
        for (auto i = 0; i < maxiter; i++) {

            // auto start = tic();

            thiscost = Estep(X, loadings.has_value(), thismu, thisSigma, thisfraction, thisPostP);

            // printf("Estep: %g \n",toc(start));

            Mstep(X, loadings.has_value(), thisPostP, regVec, thismu, thisSigma, thisfraction);

            // printf("+Mstep: %g \n",toc(start));

            if (std::abs(oldcost - thiscost) <= tol * std::abs(thiscost)) {
                break;
            }
            else
                oldcost = thiscost;
        }
        if (thiscost < cost) {
            cost = thiscost;
            PermutationMatrix P = sortPermMat(thisfraction); // order by fraction
            VectorXi permVec = P.indices();
            mu = thismu * P;
            postP = thisPostP * P;
            for (auto i = 0; i < nClust; i++)
                Sigma.block(0, i * nFeatures, nFeatures, nFeatures) =
                thisSigma.block(0, permVec(i) * nFeatures, nFeatures, nFeatures);
        }
    }

    if (loadings.has_value())
        Centroid = loadings.value() * mu;
    else
        Centroid = mu;

    // Make the cluster assignments
    rowmax2index(postP, clustID);

    return cost;
}

// Overloaded function to analyze full dataset
template <typename derived, typename derivedI,
    typename T = typename MatrixBase<derived>::Scalar >
T trainModel(const MatrixBase<derived>& X,
    const int nClust, const int nReplicates, const T regParam,
    MatrixBase<derived>& Centroid, MatrixBase<derived>& postP,
    MatrixBase<derivedI>& clustID, MatrixBase<derived>& mu, MatrixBase<derived>& Sigma)
{
    std::optional<Matrix<T, Dynamic, Dynamic>> P = std::nullopt;
    T cost = trainModel(X, P, nClust, nReplicates, regParam, Centroid, postP, clustID, mu, Sigma);
    return cost;
}

// Overloaded function for analyzing PCA representation of data
// This assumes the scores and loading matrices are "tall"
template <typename derivedR, typename derivedI,
    typename T = typename MatrixBase<derivedR>::Scalar >
T trainModel(const MatrixBase<derivedR>& scores, const MatrixBase<derivedR>& loadings,
    const int nClust, const int nReplicates, const T regParam,
    MatrixBase<derivedR>& Centroid, MatrixBase<derivedR>& postP,
    MatrixBase<derivedI>& clustID, MatrixBase<derivedR>& mu, MatrixBase<derivedR>& Sigma)
{
    std::optional<Matrix<T, Dynamic, Dynamic>> P = loadings;
    T cost = trainModel(scores, P, nClust, nReplicates, regParam, Centroid, postP, clustID, mu, Sigma);
    return cost;
}

#endif /* __GMM_H__ */   